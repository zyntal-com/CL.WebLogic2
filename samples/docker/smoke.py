#!/usr/bin/env python3
"""
End-to-end smoke test for the WebLogic sample sites (Python standard library only).

  python smoke.py starter  http://localhost:5080 --mailpit http://localhost:8025 --admin-password ...
  python smoke.py miniblog http://localhost:5081 --admin-password ...

It drives the sites the way a browser does: real forms (CSRF field, honeypot, signed timestamp),
cookies, redirects, e-mailed links (through Mailpit), TOTP codes, API keys, server-sent events.
Exits non-zero when any check fails.
"""
import argparse
import base64
import hashlib
import hmac
import html
import http.cookiejar
import json
import re
import secrets
import struct
import sys
import threading
import time
import urllib.error
import urllib.parse
import urllib.request

FAILURES = []


def check(name, condition, detail=""):
    print(("  ok   " if condition else "  FAIL ") + name + ("" if condition or not detail else f"  -- {detail}"))
    if not condition:
        FAILURES.append(name)
    return condition


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *args, **kwargs):
        return None


class Response:
    def __init__(self, status, headers, body):
        self.status, self.headers, self.body = status, headers, body

    @property
    def text(self):
        return self.body.decode("utf-8", "replace")

    def json(self):
        return json.loads(self.body)

    @property
    def location(self):
        return self.headers.get("Location", "")


class Client:
    """A cookie-keeping client that does not follow redirects (so they can be checked)."""

    def __init__(self, base):
        self.base = base.rstrip("/")
        self.jar = http.cookiejar.CookieJar()
        self.opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(self.jar), NoRedirect())

    def request(self, method, path, data=None, headers=None, timeout=20):
        url = path if path.startswith("http") else self.base + path
        req = urllib.request.Request(url, data=data, method=method, headers=headers or {})
        try:
            with self.opener.open(req, timeout=timeout) as r:
                return Response(r.status, r.headers, r.read())
        except urllib.error.HTTPError as e:
            return Response(e.code, e.headers, e.read())

    def get(self, path, headers=None):
        return self.request("GET", path, headers=headers)

    def post(self, path, fields, headers=None):
        return self.request("POST", path, urllib.parse.urlencode(fields, doseq=True).encode(),
                            {"Content-Type": "application/x-www-form-urlencoded", **(headers or {})})

    def post_json(self, path, payload, headers=None, method="POST"):
        return self.request(method, path, json.dumps(payload).encode(), {"Content-Type": "application/json", **(headers or {})})

    def csrf(self):
        return self.get("/_weblogic/csrf").json()["token"]

    def follow(self, response, limit=5):
        while response.status in (301, 302, 303, 307, 308) and limit > 0:
            response, limit = self.get(response.location), limit - 1
        return response

    def submit(self, page, form_id, fields, action=None, body=None):
        """Loads the page (or uses `body`), keeps the form's hidden fields, waits out the anti-bot timer and posts."""
        body = body if body is not None else self.get(page).text
        match = re.search(r'<form id="wl-form-' + re.escape(form_id) + r'".*?</form>', body, re.S)
        if not match:
            raise AssertionError(f"form '{form_id}' not found on {page}")
        form = match.group(0)
        hidden = {html.unescape(n): html.unescape(v) for n, v in re.findall(r'<input type="hidden" name="([^"]+)" value="([^"]*)"', form)}
        if "__wl_ts" in hidden:
            time.sleep(2.2)  # Forms.MinSubmitSeconds
        target = action or html.unescape(re.search(r'action="([^"]*)"', form).group(1))
        return self.post(target, {**hidden, "__wl_hp": "", **fields})


def totp(secret, at=None):
    key = base64.b32decode(secret.replace(" ", "").upper() + "=" * (-len(secret) % 8))
    counter = struct.pack(">Q", int((at or time.time()) // 30))
    digest = hmac.new(key, counter, hashlib.sha1).digest()
    offset = digest[-1] & 0x0F
    return f"{(struct.unpack('>I', digest[offset:offset + 4])[0] & 0x7FFFFFFF) % 1_000_000:06d}"


def mailpit_link(mailpit, to, path):
    """The newest link to `path` in a message sent to `to` (waits for it)."""
    for _ in range(40):
        try:
            search = json.load(urllib.request.urlopen(f"{mailpit}/api/v1/search?query=" + urllib.parse.quote(f'to:"{to}"'), timeout=5))
            for message in search.get("messages", []):
                full = json.load(urllib.request.urlopen(f"{mailpit}/api/v1/message/{message['ID']}", timeout=5))
                found = re.search(r"https?://[^\s\"<>]+" + re.escape(path) + r"\?token=[^\s\"<>]+", full.get("Text", "") + full.get("HTML", ""))
                if found:
                    link = urllib.parse.urlparse(html.unescape(found.group(0)))
                    return link.path + "?" + link.query
        except OSError:
            pass
        time.sleep(0.5)
    return None


def read_sse(client, path, wanted, seconds, result):
    """Collects event types from an SSE stream until `wanted` arrives (runs in a thread)."""
    req = urllib.request.Request(client.base + path, headers={"Accept": "text/event-stream"})
    try:
        with client.opener.open(req, timeout=seconds) as stream:
            deadline = time.time() + seconds
            while time.time() < deadline:
                line = stream.readline().decode("utf-8", "replace").strip()
                if line.startswith("data: "):
                    event = json.loads(line[6:])
                    result.append(event)
                    if event.get("type") == wanted:
                        return
    except OSError as e:
        result.append({"type": "error", "data": str(e)})


def common(client, name, logout):
    print(f"[{name}] platform")
    home = client.get("/")
    check("home page renders", home.status == 200, home.status)
    csp = home.headers.get("Content-Security-Policy", "")
    check("CSP with nonce and strict-dynamic", "'nonce-" in csp and "'strict-dynamic'" in csp, csp)
    check("nosniff header", home.headers.get("X-Content-Type-Options") == "nosniff")
    check("no Server header", home.headers.get("Server") is None, home.headers.get("Server"))
    check("liveness probe", client.get("/_weblogic/health/live").status == 200)
    check("readiness probe", client.get("/_weblogic/health/ready").status == 200)
    check("theme templates are not served", client.get("/assets/templates/home.html").status == 404)
    check("path traversal is refused", client.get("/assets/..%2f..%2fappsettings.json").status in (400, 404))
    check("themed 404", client.get("/no-such-page").status == 404)
    anon_post = client.post(logout, {})
    check("POST without CSRF token is refused", anon_post.status == 403, anon_post.status)


def starter(args):
    c = Client(args.base)
    common(c, "starter", "/account/logout")
    user = "smoke" + secrets.token_hex(3)
    email = f"{user}@example.test"
    password = "Smoke-Test-Password-1"

    print("[starter] registration and e-mail verification")
    r = c.submit("/account/register", "register", {"UserName": user, "Email": email, "Password": password, "ConfirmPassword": password})
    check("register redirects to the account", r.status == 303 and r.location.startswith("/account"), f"{r.status} {r.location} {r.text[:300]}")
    check("account page shows the user", user in c.get("/account").text)
    if args.mailpit:
        link = mailpit_link(args.mailpit, email, "/account/verify-email")
        check("verification e-mail arrived", link is not None)
        if link:
            check("verification link confirms", "confirmed" in c.get(link).text)
            check("verification link works once", "invalid" in c.get(link).text)

    print("[starter] two-factor")
    setup = c.get("/account/two-factor/setup").text
    secret = re.search(r'<code class="user-select-all">([A-Z2-7]+)</code>', setup)
    check("enrollment shows a secret and QR", secret is not None and "data:image/png;base64," in setup)
    r = c.submit("/account/two-factor/setup", "two-factor-setup", {"Code": totp(secret.group(1))}, body=setup)
    codes = re.findall(r"<li><code>([^<]+)</code></li>", r.text)
    check("enrollment returns recovery codes", r.status == 200 and len(codes) >= 8, f"{r.status} {len(codes)}")

    c.post("/account/logout", {"__wl_csrf": c.csrf()})
    check("signed out", c.get("/account").status == 302)
    r = c.submit("/account/login", "login", {"Login": user, "Password": "wrong-password-xx"})
    check("wrong password is refused", r.status == 422 and "Wrong user name or password" in r.text, r.status)
    r = c.submit("/account/login", "login", {"Login": user, "Password": password, "RememberMe": "true"})
    check("password step asks for the second factor", r.status == 303 and r.location.startswith("/account/two-factor"), f"{r.status} {r.location}")
    check("not signed in before the second factor", c.get("/account").status == 302)
    r = c.submit("/account/recovery-code", "recovery-code", {"Code": codes[0]})
    check("recovery code completes sign-in", r.status == 303 and c.get("/account").status == 200, f"{r.status} {r.location}")
    session_cookie = next((k for k in c.jar if k.name.endswith("wl_sid")), None)
    check("remember me survives the second factor (persistent cookie)", session_cookie is not None and session_cookie.expires is not None)
    account = c.get("/account").text
    check("account page lists this device", "this browser" in account and "remembered" in account)

    print("[starter] API keys")
    r = c.submit("/account", "api-key", {"Name": "smoke", "Scopes": ["profile:read", "wall:write"], "ExpiresInDays": "1"}, action="/account/api-keys")
    key = re.search(r"(wl_[A-Za-z0-9_-]+)</code>", r.text)
    check("key is shown once", key is not None, r.text[:300])
    api = Client(args.base)
    me = api.get("/api/v1/me", {"X-Api-Key": key.group(1)})
    check("/api/v1/me with the key", me.status == 200 and me.json().get("name") == user, f"{me.status} {me.text[:200]}")
    check("/api/v1/me without a key is 401", api.get("/api/v1/me").status == 401)
    check("/api/v1/me with a session is 403 (keys only)", c.get("/api/v1/me").status == 403)

    print("[starter] realtime")
    events = []
    reader = threading.Thread(target=read_sse, args=(Client(args.base), "/_weblogic/realtime/sse", "starter.shout", 10, events))
    reader.start()
    time.sleep(1.0)
    posted = api.post_json("/api/v1/wall", {"text": "hello from smoke"}, {"X-Api-Key": key.group(1)})
    check("API post to the wall", posted.status == 202, f"{posted.status} {posted.text[:200]}")
    reader.join(12)
    shout = next((e for e in events if e.get("type") == "starter.shout"), None)
    check("SSE delivers the message to anonymous listeners", shout is not None and shout["data"]["text"] == "hello from smoke", events[:3])

    print("[starter] dashboard")
    token = c.csrf()
    layout = c.get("/_weblogic/dashboard/home").json()
    items = layout["items"] or []
    saved = c.post_json("/_weblogic/dashboard/home", {"version": layout["version"], "items": items + [{"widgetName": "clock", "zone": "main"}]}, {"X-CSRF-Token": token}, method="PUT")
    check("layout saves with a version", saved.status == 200 and saved.json()["version"] == layout["version"] + 1, f"{saved.status} {saved.text[:200]}")
    stale = c.post_json("/_weblogic/dashboard/home", {"version": layout["version"], "items": []}, {"X-CSRF-Token": token}, method="PUT")
    check("stale version is a conflict", stale.status == 409, stale.status)
    check("dashboard page renders widgets", 'data-wl-widget="clock"' in c.get("/dashboard").text)

    print("[starter] widget settings")
    current = c.get("/_weblogic/dashboard/home").json()
    with_note = c.post_json("/_weblogic/dashboard/home", {"version": current["version"], "items": current["items"] + [{"widgetName": "notes", "zone": "main"}]},
                            {"X-CSRF-Token": token}, method="PUT")
    note_id = next((i["instanceId"] for i in (with_note.json().get("items") or []) if i["widgetName"] == "notes"), None) if with_note.status == 200 else None
    check("note widget added to the dashboard", note_id is not None, f"{with_note.status} {with_note.text[:200]}")
    if note_id:
        saved_note = c.post_json(f"/_weblogic/widgets/notes/actions/save?instance={note_id}", {"text": "smoke note"}, {"X-CSRF-Token": token})
        check("note saves", saved_note.status == 200, f"{saved_note.status} {saved_note.text[:200]}")
        check("note is read back", "smoke note" in c.get(f"/_weblogic/widgets/notes?instance={note_id}").text)
        args.note_instance = note_id

    print("[starter] forms")
    bad = c.submit("/forms", "event-signup", {"Name": "", "Email": "nope"})
    check("invalid gallery post returns 422", bad.status == 422, bad.status)
    good = c.submit("/forms", "event-signup", {"Name": "Ada", "Email": "ada@example.test", "Country": "dk", "Attendance": "in-person",
                                               "Arrival": "2026-11-20", "Experience": "4", "AcceptConduct": "true"})
    check("valid gallery post redirects", good.status == 303, f"{good.status} {good.text[:300]}")

    print("[starter] caching")
    anon = Client(args.base)
    first = re.search(r"data-rendered-at>([^<]+)<", anon.get("/cache").text).group(1)
    second = re.search(r"data-rendered-at>([^<]+)<", anon.get("/cache").text).group(1)
    check("anonymous page comes from the output cache", first == second, f"{first} vs {second}")
    signed = re.search(r"data-rendered-at>([^<]+)<", c.get("/cache").text).group(1)
    check("signed-in visitors get a fresh render", signed != first)
    evicted = anon.request("POST", "/cache/evict", b"", {"X-CSRF-Token": anon.csrf()})
    check("tag eviction", evicted.status == 204, evicted.status)
    third = re.search(r"data-rendered-at>([^<]+)<", anon.get("/cache").text).group(1)
    check("evicted page renders again", third != first)

    if args.admin_password:
        print("[starter] operator")
        admin = Client(args.base)
        r = admin.submit("/account/login", "login", {"Login": "admin", "Password": args.admin_password})
        check("admin signs in", r.status == 303, f"{r.status} {r.text[:200]}")
        if getattr(args, "note_instance", None):
            others = admin.get(f"/_weblogic/widgets/notes?instance={args.note_instance}")
            check("another user cannot read the note", "smoke note" not in others.text, others.status)
        explorer = admin.get("/_weblogic/explorer.json")
        anonymous_explorer = Client(args.base).get("/_weblogic/explorer.json")
        check("explorer is closed to anonymous visitors", anonymous_explorer.status in (401, 404), anonymous_explorer.status)
        if explorer.status == 200:
            routes = explorer.json()["anonymousRoutes"]
            check("explorer lists the anonymous routes", "GET /" in routes and not any("/account/api-keys" in r for r in routes), routes)


def miniblog(args):
    c = Client(args.base)
    common(c, "miniblog", "/logout")
    print("[miniblog] reading")
    home = c.get("/").text
    posts = re.findall(r'href="(/posts/[a-z0-9-]+)"', home)
    check("home lists posts", len(posts) > 0)
    if posts:
        post = c.get(posts[0])
        check("post page renders markdown", post.status == 200 and "<article" in post.text, post.status)
    feed = c.get("/feed.xml")
    check("RSS feed", feed.status == 200 and "<rss" in feed.text and "xml" in feed.headers.get("Content-Type", ""), feed.status)
    check("sitemap", "<urlset" in c.get("/sitemap.xml").text)
    check("tag page", c.get("/tags/weblogic").status == 200)
    check("admin requires sign-in", c.get("/admin").status == 302)

    if args.admin_password:
        print("[miniblog] admin plugin")
        r = c.submit("/login", "login", {"Login": "admin", "Password": args.admin_password})
        check("admin signs in", r.status == 303, f"{r.status} {r.text[:200]}")
        check("admin dashboard", c.get("/admin").status == 200)
        title = "Smoke post " + secrets.token_hex(3)
        r = c.submit("/admin/posts/new", "post-editor", {"Title": title, "Tags": "smoke, weblogic", "Body": "Hello **smoke**.\n\n<script>alert(1)</script>", "Published": "true"})
        check("post is created", r.status == 303, f"{r.status} {r.text[:300]}")
        check("new post shows on the cached home page", title in Client(args.base).get("/").text)
        slug = re.search(r'href="(/posts/smoke-post-[a-z0-9-]+)"', Client(args.base).get("/").text)
        if check("new post has a slug", slug is not None):
            body = Client(args.base).get(slug.group(1)).text
            check("markdown rendered and script removed", "<strong>smoke</strong>" in body and "<script>alert(1)</script>" not in body)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("site", choices=["starter", "miniblog"])
    parser.add_argument("base")
    parser.add_argument("--mailpit")
    parser.add_argument("--admin-password")
    args = parser.parse_args()
    try:
        (starter if args.site == "starter" else miniblog)(args)
    except Exception as e:  # a crash in the script is a failure too
        check("smoke test ran to the end", False, repr(e))
    print(f"\n{args.site}: {'FAILED: ' + ', '.join(FAILURES) if FAILURES else 'all checks passed'}")
    sys.exit(1 if FAILURES else 0)


if __name__ == "__main__":
    main()

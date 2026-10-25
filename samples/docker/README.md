# WebLogic demo in Docker

Two sample sites built on CL.WebLogic, ready to run locally or deploy to a server:

| Service | What it shows | Local URL |
|---|---|---|
| `starter` | The reference site: accounts (verification, password reset, TOTP two-factor with recovery codes, API keys, optional Steam sign-in), forms gallery and wizard, per-user dashboards, live wall over SSE, output and fragment caching, the API explorer. Persistence: JSON files on its volume. | http://localhost:5080 |
| `miniblog` | A blog on MySQL (CL.MySQL2): cached public pages, RSS and sitemap, MySQL-backed accounts and sessions, and an admin area that is a **plugin** loaded at startup. | http://localhost:5081 |
| `mysql` | MySQL 8.4 for MiniBlog. | 127.0.0.1:3307 (local dev only) |
| `mailpit` | Catches every security e-mail (verification, reset links). | http://localhost:8025 |
| `proxy` (optional) | Caddy with automatic HTTPS in front of both sites. | https://`$STARTER_DOMAIN`, https://`$BLOG_DOMAIN` |

## Run it

```bash
cd samples/docker
cp .env.example .env          # optional: set admin passwords, ports, domains
docker compose up -d --build --wait
```

- Sign in as `admin` on either site. The password comes from `STARTER_ADMIN_PASSWORD` or `MINIBLOG_ADMIN_PASSWORD`. If it is empty, one is generated on first start and printed once by `docker compose logs starter` or `docker compose logs miniblog`.
- Passwords must be at least 10 characters and must not contain the user name or e-mail address.
- Register a normal account on the starter site, then open Mailpit to click the verification link.
- Reset everything with `docker compose down -v`.

The images are built from this repository. The Dockerfile packs `CL.WebLogic` into a local NuGet feed and publishes the sample against that package, the same way an application would consume it.

## Deploy it on a server

1. Point two DNS names at the server, for example `starter.example.com` and `blog.example.com`.
2. In `.env`, set:
   ```env
   STARTER_DOMAIN=starter.example.com
   BLOG_DOMAIN=blog.example.com
   STARTER_PUBLIC_URL=https://starter.example.com
   MINIBLOG_PUBLIC_URL=https://blog.example.com
   TRUSTED_PROXIES=172.30.0.10      # the Caddy container (fixed IP on the compose network)
   DEMO_HTTPS=true                  # redirect to HTTPS and send HSTS
   STARTER_ADMIN_PASSWORD=…         # or read the generated ones from the logs
   MINIBLOG_ADMIN_PASSWORD=…
   MINIBLOG_PASSWORD_PEPPER=…       # a long random secret; keep it with the database backups
   MYSQL_PASSWORD=…
   MYSQL_ROOT_PASSWORD=…
   ```
3. Start with the proxy: `docker compose --profile proxy up -d --build --wait`. Caddy gets certificates from Let's Encrypt.
4. Remove the `ports:` entries of `starter`, `miniblog`, `mysql` and `mailpit` from `compose.yaml` (or bind them to `127.0.0.1`), so only Caddy is reachable from outside. For real mail, replace Mailpit with your SMTP server (`SMTP_HOST`, `SMTP_PORT`, `SMTP_USER`, `SMTP_PASSWORD`, `SMTP_SECURITY`).

Behind the proxy, `X-Forwarded-*` headers are trusted only from `TRUSTED_PROXIES`, so sending them straight to an app port has no effect. Over HTTPS the cookies switch to `__Host-` names with `Secure`.

## How the containers are set up

- They run in Production as a non-root user, with a read-only root filesystem, all capabilities dropped and `no-new-privileges`. WebLogic's development-store guard would refuse to start if a security store were still in memory.
- All state lives in one volume per app, mounted at `/data`:
  - CodeLogic config and logs
  - the app's data
  - DataProtection keys, which keep sessions, tokens and 2FA secrets valid across restarts
  - MiniBlog's plugins folder
- The environment is written into the CodeLogic config files at startup (`samples/Shared/SampleConfig.cs`). Values that aren't set leave the files alone, so you can also edit them in the volume.
- On first start, a password pepper and an IP-hash key are generated into the config.
  - MiniBlog's users live in MySQL. Set `MINIBLOG_PASSWORD_PEPPER`, or keep the `miniblog-data` volume together with the MySQL data. Without the pepper the hashes stop verifying.
- Health checks run `dotnet <App>.dll --probe` against `/_weblogic/health/live`, so no curl is needed in the image.

| Variable | Used by | Meaning |
|---|---|---|
| `DEMO_PUBLIC_URL` | both | Canonical base URL for e-mail links, redirects, RSS. Never derived from the Host header. |
| `DEMO_ADMIN_PASSWORD` | both | The first admin's password (only used when the account does not exist yet). |
| `DEMO_ALLOWED_HOSTS` | both | Comma-separated host names; other hosts get 400. |
| `DEMO_TRUSTED_PROXIES` | both | Proxy IPs/CIDRs allowed to set `X-Forwarded-*` (turns forwarded headers on). |
| `DEMO_HTTPS` | both | Redirect to HTTPS and send HSTS. |
| `DEMO_EXPLORER` | both | Serve `/_weblogic/explorer` in production, only to accounts with the `weblogic.explorer` permission (the admins). |
| `DEMO_PASSWORD_PEPPER` | both | Secret mixed into password hashes. |
| `SMTP_*`, `DEMO_MAIL_FROM` | both | Outgoing mail (CL.Mail). |
| `STEAM_SIGNIN` | starter | Offer "Sign in through Steam". |
| `MINIBLOG_MYSQL_*` | miniblog | MySQL host, port, database, user, password. |

## Test it

`smoke.py` (Python standard library only) drives both sites the way a browser does. CI runs it on every push (`.github/workflows/ci-tests.yml`, job `samples`):

```bash
python smoke.py starter  http://localhost:5080 --mailpit http://localhost:8025 --admin-password "$STARTER_ADMIN_PASSWORD"
python smoke.py miniblog http://localhost:5081 --admin-password "$MINIBLOG_ADMIN_PASSWORD"
```

## Run without Docker

```bash
dotnet run --project samples/StarterWebsite          # http://localhost:5080, Development, mails to data/…/outbox
docker compose -f samples/docker/compose.yaml up -d mysql
dotnet run --project samples/MiniBlog                # http://localhost:5081 (MySQL on 127.0.0.1:3307)
```

Under `dotnet run`, the samples reference `src/CL.WebLogic` directly, so you can edit the toolkit and a sample together. Build with `-p:WebLogicFromPackage=true` to use the package instead.

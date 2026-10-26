# Gaming community (Steam)

A community site for a game or server: players sign in with Steam, see who is online, chat on a live
wall, and admins post announcements. The StarterWebsite has the Steam sign-in and the live wall
(`STEAM_SIGNIN=true` in the Docker demo).

## Steam sign-in

The `CodeLogic.WebLogic.ExternalLogins` add-on has a complete provider for Steam's OpenID 2.0 (no API
key needed; the assertion is verified by posting it back to Steam). Reference the package and register
it ([External logins](../guides/external-logins.md)):

```csharp
web.AddSteamLogin();
```

Two routes start and finish the flow:

```csharp
account.MapPage("/external/{provider}", ChallengeAsync).AllowAnonymous();
account.MapPage("/external/{provider}/callback", CallbackAsync).AllowAnonymous();

async ValueTask<WebResult> CallbackAsync(WebRequestContext r, CancellationToken ct)
{
    var outcome = await external.HandleCallbackAsync(r, r.GetRouteValue("provider")!, ct);
    if (outcome.IsFailure) return WebResult.Page("templates/account/external-failed");

    var (info, user, returnUrl) = (outcome.Value!.Info, outcome.Value.User, outcome.Value.ReturnUrl);
    if (user is null)
    {
        var created = await accounts.RegisterAsync(new WebRegistration
        {
            UserName = $"steam-{info.ProviderKey}", Groups = ["players"], SendVerificationEmail = false
        }, ct);
        user = created.Value!;
        await external.LinkAsync(user, info, ct);
    }
    // Applies the account rules and asks two-factor users for their code.
    var result = await r.Auth.SignInExternalAsync(user, "steam", persistent: true, ct);
    return result.Status == WebSignInStatus.RequiresTwoFactor
        ? WebResult.Redirect("/account/two-factor?returnUrl=" + Uri.EscapeDataString(returnUrl))
        : result.Status == WebSignInStatus.Succeeded ? WebResult.Redirect(returnUrl) : WebResult.Page("templates/account/external-failed");
}
```

`WebExternalLoginFlow` keeps state and expiry in a protected `__Host-` cookie and only returns to local
URLs. Players can add a passkey afterwards and skip Steam on their next visit ([Passkeys](../guides/passkeys.md)).
`SignInExternalAsync` (not `SignInAsync`) makes players who turned on two-factor authentication
enter their code, records `ext:steam` as the sign-in method, and `persistent: true` keeps them
signed in across browser restarts.

Your user store implements `IWebUserExternalLoginStore` to find users by Steam id. Steam gives no
e-mail address, so password reset does not apply to these accounts; they sign in through Steam only.

## Live features

- **Who is online:** `WebRealtimeHub.ConnectionCount`, or track user ids on connect in your app.
- **Live wall / chat:** a form or WebSocket message saves the post, then
  `hub.PublishAsync("wall.post", new { author, text }, WebRealtimeAudience.Authenticated)`. Encode on
  the client side too: render with `WebLogic.dom.el`, never `innerHTML`.
- **Server status widget:** a widget with `RefreshSeconds = 30` that queries your game server; cache the
  query with the fragment cache so many viewers cause one query.
- **Announcements:** admins publish to `WebRealtimeAudience.Public`; bridge game events from CodeLogic
  with `web.BridgeEvent<T>()`.

## Abuse controls

- Rate-limit posting (`WithRateLimit("wall")`, partition `user`).
- Anti-bot fields stay on for anonymous forms.
- Moderators: a `moderators` group, a remove endpoint, and `hub.CloseUser(userId)` to drop a banned
  player's live connections immediately (also mark the user disabled so they cannot sign in again).

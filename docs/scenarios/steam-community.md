# Gaming community (Steam)

A community site for a game or server: players sign in with Steam, see who is online, chat on a live
wall, and admins post announcements. The StarterWebsite has the Steam sign-in and the live wall
(`STEAM_SIGNIN=true` in the Docker demo).

## Steam sign-in

`samples/StarterWebsite/Steam/SteamOpenIdProvider.cs` is a complete `IWebExternalLoginProvider` for
Steam's OpenID 2.0 (no API key needed; the assertion is verified by posting it back to Steam). Copy it
into your app and register it:

```csharp
web.Services.AddHttpClient(SteamOpenIdProvider.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(10));
web.AddExternalLoginProvider<SteamOpenIdProvider>();
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
    await r.Auth.SignInAsync(user, ["ext:steam"], ct);
    return WebResult.Redirect(returnUrl);
}
```

`WebExternalLoginFlow` keeps state and expiry in a protected `__Host-` cookie and only returns to local
URLs. Your user store implements `IWebUserExternalLoginStore` to find users by Steam id. Steam gives no
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

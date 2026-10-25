# Testing

`CodeLogic.WebLogic.Testing` runs your WebLogic setup on a real Kestrel server bound to
`127.0.0.1:0`, with a cookie-aware `HttpClient`, so tests go through the full pipeline (security
stages, CSRF, sessions, output cache) exactly as production requests do. It needs no ASP.NET
TestHost package.

```csharp
using CL.WebLogic.Testing.Services;

public sealed class ContactPageTests
{
    [Fact]
    public async Task Anonymous_post_without_a_csrf_token_is_refused()
    {
        await using var host = await WebLogicTestHost.StartAsync(new WebLogicTestHostOptions
        {
            ConfigureWebLogic = web =>
            {
                web.UseInMemoryAccounts();
                web.AddForm<ContactForm>();
                web.AddContributor(new DelegateContributor(b =>
                    b.MapPage("/contact", (r, _) => ValueTask.FromResult<WebResult>(WebResult.Text("ok")), "POST").AllowAnonymous()));
            }
        });

        var response = await host.Client.PostAsync("/contact", new FormUrlEncodedContent([]));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
```

| Member | Use |
|---|---|
| `WebLogicTestHostOptions.Config` | A `WebLogicConfig` to start with (e.g. trusted proxies, rate limits) |
| `EnvironmentName`, `AllowDevelopmentStores` | Test production behaviour (secure cookies, store guard) |
| `ConfigureWebLogic`, `ConfigureServices`, `ConfigureApp` | Your registrations, extra services, extra middleware or minimal APIs |
| `host.Client`, `host.CreateClient(allowAutoRedirect)` | Clients sharing a cookie container / fresh ones |
| `host.WriteContentFile(path, content)` | Write theme files into the temporary content root |
| `host.Services`, `host.Context` | The service provider and the fake CodeLogic context (`RecordingLogger`) |

Other helpers: `TemplateTestRenderer` + `InMemoryTemplateLoader` render templates without a host,
`ManualTimeProvider` controls time (lockouts, token expiry, sessions), `DelegateContributor` maps routes
inline.

## What to test in your app

- Your stores against the contracts: atomic `RecordFailedAccessAsync`, `TryConsumeAsync` on recovery
  codes, `SaveAsync(layout, expectedVersion)` conflicts, `DeleteForUserAsync` on sessions.
- That pages meant to be private are not `AllowAnonymous()` (the explorer JSON at
  `/_weblogic/explorer.json` lists the anonymous routes; assert on it).
- Your handlers' authorization for resources (user A cannot edit user B's order).

## Client tests

`/_weblogic/tests` runs the client's browser suite (Development). The toolkit's own CI runs it in
headless Chrome.

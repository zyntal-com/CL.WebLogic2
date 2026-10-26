# CodeLogic.WebLogic.Testing

Test helpers for apps built on [CodeLogic.WebLogic](https://www.nuget.org/packages/CodeLogic.WebLogic).

`WebLogicTestHost` runs your WebLogic setup on a real Kestrel server bound to `127.0.0.1:0` with a
cookie-aware `HttpClient`, so tests go through the full pipeline — security stages, CSRF, sessions,
output cache — exactly like production requests. No ASP.NET TestHost package needed.

```csharp
using CL.WebLogic.Testing.Services;

[Fact]
public async Task Anonymous_post_without_a_csrf_token_is_refused()
{
    await using var host = await WebLogicTestHost.StartAsync(new WebLogicTestHostOptions
    {
        ConfigureWebLogic = web =>
        {
            web.UseInMemoryAccounts();
            web.AddContributor(new DelegateContributor(b =>
                b.MapPage("/contact", (r, _) => ValueTask.FromResult<WebResult>(WebResult.Text("ok")), "POST").AllowAnonymous()));
        }
    });

    var response = await host.Client.PostAsync("/contact", new FormUrlEncodedContent([]));

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
}
```

## What's inside

- `WebLogicTestHost` / `WebLogicTestHostOptions`: config, environment (test production behaviour),
  your WebLogic registrations, extra services and middleware; theme files via `WriteContentFile`.
- `DelegateContributor`: map routes inline in a test.
- `TemplateTestRenderer` + `InMemoryTemplateLoader`: render templates without a host.
- `ManualTimeProvider`: control time for lockouts, token expiry and sessions.
- `WebTestAuthenticator`: a software passkey authenticator; turns your passkey endpoints' options into
  the JSON a browser posts back, with real keys, signatures and attestation (none, packed, fido-u2f).
- `FakeWebLogicContext` + `RecordingLogger`: assert on what the toolkit logged.

Works with any test framework. No third-party dependencies beyond CodeLogic.*.

Docs: https://zyntal-com.github.io/CL.WebLogic2/guides/testing.html

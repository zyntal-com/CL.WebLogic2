using CL.WebLogic.IntegrationTests.Auth;

namespace CL.WebLogic.IntegrationTests.Client;

/// <summary>
/// Passkeys in a real browser: headless Chrome with a virtual WebAuthn authenticator (DevTools
/// protocol) runs the client's <c>WebLogic.passkeys</c> against the server. Proves the JSON
/// encoding, client data, RP id and origin handling interoperate with an actual WebAuthn stack.
/// </summary>
public sealed class PasskeyBrowserTests
{
    private const string Page = """
        <!doctype html><html><head><title>pending</title></head><body><main data-wl-shell>passkeys</main>{weblogic_scripts}
        <script type="module">
          const steps = [];
          const wl = window.WebLogic;
          try {
            await wl.http.post('/auth/login', { login: 'alice', password: 'PASSWORD' });
            steps.push('login');
            steps.push((await wl.passkeys.register('/pk/register/begin', '/pk/register/finish', { name: 'Chrome' })).status);
            await wl.http.post('/auth/logout', {});
            steps.push((await wl.passkeys.signIn('/pk/signin/begin', '/pk/signin/finish', { remember: true })).status);
            steps.push((await wl.http.get('/me')).data.methods);
            steps.push((await wl.passkeys.verify('/pk/reauth/begin', '/pk/reauth/finish')).status);
            document.title = 'DONE ' + steps.join('|');
          } catch (e) {
            document.title = 'FAIL ' + steps.join('|') + ' ' + e.name + ': ' + e.message + (e.problem ? ' ' + JSON.stringify(e.problem) : '');
          }
        </script></body></html>
        """;

    [Fact]
    public async Task Register_sign_in_and_step_up_with_a_virtual_authenticator()
    {
        var browser = ClientRuntimeTests.FindBrowser();
        if (browser is null)
            return; // no Chrome/Edge on this machine; CI provides one

        // No PublicBaseUrl: RP id and origin come from the request (localhost).
        await using var host = await PasskeyTestSite.StartAsync(c => c.PublicBaseUrl = "", page: true);
        host.WriteContentFile("theme/templates/passkeys.html", Page.Replace("PASSWORD", AuthTestSite.Password, StringComparison.Ordinal));
        await AuthTestSite.CreateUserAsync(host);

        await using var chrome = await ChromeDevTools.LaunchAsync(browser);
        var target = await chrome.SendAsync("Target.createTarget", new { url = "about:blank" });
        var session = (await chrome.SendAsync("Target.attachToTarget", new { targetId = target.GetProperty("targetId").GetString(), flatten = true }))
            .GetProperty("sessionId").GetString()!;
        await chrome.SendAsync("WebAuthn.enable", new { enableUI = false }, session);
        var authenticator = (await chrome.SendAsync("WebAuthn.addVirtualAuthenticator", new
        {
            options = new { protocol = "ctap2", transport = "internal", hasResidentKey = true, hasUserVerification = true, isUserVerified = true, automaticPresenceSimulation = true }
        }, session)).GetProperty("authenticatorId").GetString();

        await chrome.SendAsync("Page.navigate", new { url = $"http://localhost:{host.BaseAddress.Port}/passkeys" }, session);
        var title = "";
        for (var i = 0; i < 120 && !title.StartsWith("DONE", StringComparison.Ordinal) && !title.StartsWith("FAIL", StringComparison.Ordinal); i++)
        {
            await Task.Delay(250);
            title = (await chrome.EvaluateAsync(session, "document.title")).GetString() ?? "";
        }

        Assert.Equal("DONE login|Registered:Chrome|Succeeded|passkey|Succeeded", title);
        var credentials = await chrome.SendAsync("WebAuthn.getCredentials", new { authenticatorId = authenticator }, session);
        var credential = Assert.Single(credentials.GetProperty("credentials").EnumerateArray());
        Assert.True(credential.GetProperty("isResidentCredential").GetBoolean());
        Assert.Equal("localhost", credential.GetProperty("rpId").GetString());
    }
}

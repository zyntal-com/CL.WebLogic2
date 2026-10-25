using System.Net;
using System.Text.Json;
using CL.WebLogic.Auth.ApiKeys;
using CL.WebLogic.Auth.Handlers;
using CL.WebLogic.Configuration.Models;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Security.Identity;
using CL.WebLogic.Security.Pipeline;
using CL.WebLogic.Testing.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CL.WebLogic.IntegrationTests.Auth;

public sealed class ApiAuthenticationTests
{
    private static async Task<string> CreateKeyAsync(WebLogicTestHost host, WebApiKeyRequest? request = null) =>
        (await host.Services.GetRequiredService<WebApiKeyService>().CreateAsync(request ?? new WebApiKeyRequest { Name = "ci", Scopes = ["orders:read"] }, default)).PlainTextKey;

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, params (string Name, string Value)[] headers)
    {
        var request = new HttpRequestMessage(method, path);
        if (method == HttpMethod.Post)
            request.Content = new StringContent("{}");
        foreach (var (name, value) in headers)
            request.Headers.TryAddWithoutValidation(name, value);
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task ApiKey_InHeader_OrAsBearer_Authenticates()
    {
        await using var host = await AuthTestSite.StartAsync();
        var key = await CreateKeyAsync(host);
        Assert.Matches("^wl_[a-z2-9]{12}_[A-Za-z0-9_-]{43}$", key);

        foreach (var header in new[] { ("X-Api-Key", key), ("Authorization", "Bearer " + key), ("Authorization", "ApiKey " + key) })
        {
            var response = await SendAsync(host.CreateClient(), HttpMethod.Get, "/me", header);
            var me = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
            Assert.Equal("apikey", me.GetProperty("scheme").GetString());
            Assert.Equal("orders:read", me.GetProperty("scopes").GetString());
        }
    }

    [Fact]
    public async Task ApiKey_IsNotAmbient_SoPostsNeedNoCsrfToken()
    {
        await using var host = await AuthTestSite.StartAsync();
        var key = await CreateKeyAsync(host);

        Assert.Equal(HttpStatusCode.OK, (await SendAsync(host.CreateClient(), HttpMethod.Post, "/write", ("X-Api-Key", key))).StatusCode);
    }

    [Fact]
    public async Task A_users_keys_stop_working_when_the_user_is_disabled()
    {
        // Security review: keys kept authenticating after their owner was disabled or deleted.
        await using var host = await AuthTestSite.StartAsync();
        var user = await AuthTestSite.CreateUserAsync(host);
        var key = await CreateKeyAsync(host, new WebApiKeyRequest { Name = "mine", UserId = user.Id, Scopes = ["orders:read"] });
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(host.CreateClient(), HttpMethod.Get, "/me", ("X-Api-Key", key))).StatusCode);

        var users = host.Services.GetRequiredService<CL.WebLogic.Auth.Users.IWebUserStore>();
        await users.UpdateAsync((await users.FindByIdAsync(user.Id, default))! with { IsDisabled = true }, default);

        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(host.CreateClient(), HttpMethod.Get, "/me", ("X-Api-Key", key))).StatusCode);
    }

    [Fact]
    public async Task BadKeys_Fail_WithoutFallingBack()
    {
        await using var host = await AuthTestSite.StartAsync();
        var key = await CreateKeyAsync(host);
        var tampered = key[..^2] + (key[^2] == 'A' ? "BB" : "AA");

        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(host.Client, HttpMethod.Get, "/me", ("X-Api-Key", tampered))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(host.Client, HttpMethod.Get, "/me", ("X-Api-Key", "wl_garbage"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(host.Client, HttpMethod.Get, "/me", ("X-Api-Key", "wl_aaaaaaaaaaaa_" + new string('A', 43)))).StatusCode);
    }

    [Fact]
    public async Task RevokedExpiredAndOutOfNetworkKeys_Fail()
    {
        await using var host = await AuthTestSite.StartAsync();
        var service = host.Services.GetRequiredService<WebApiKeyService>();
        var revoked = await service.CreateAsync(new WebApiKeyRequest { Name = "old" }, default);
        await service.RevokeAsync(revoked.Key.Id, null, default);
        var expired = await service.CreateAsync(new WebApiKeyRequest { Name = "exp", Lifetime = TimeSpan.FromTicks(1) }, default);
        var elsewhere = await service.CreateAsync(new WebApiKeyRequest { Name = "office", AllowedNetworks = ["10.0.0.0/8"] }, default);
        var local = await service.CreateAsync(new WebApiKeyRequest { Name = "local", AllowedNetworks = ["127.0.0.1"] }, default);

        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(host.Client, HttpMethod.Get, "/me", ("X-Api-Key", revoked.PlainTextKey))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(host.Client, HttpMethod.Get, "/me", ("X-Api-Key", expired.PlainTextKey))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(host.Client, HttpMethod.Get, "/me", ("X-Api-Key", elsewhere.PlainTextKey))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(host.Client, HttpMethod.Get, "/me", ("X-Api-Key", local.PlainTextKey))).StatusCode);
    }

    [Fact]
    public async Task Scopes_AreEnforcedByPolicies()
    {
        await using var host = await AuthTestSite.StartAsync();
        var reader = await CreateKeyAsync(host);
        var other = await CreateKeyAsync(host, new WebApiKeyRequest { Name = "other", Scopes = ["users:read"] });

        Assert.Equal(HttpStatusCode.OK, (await SendAsync(host.Client, HttpMethod.Get, "/scoped", ("X-Api-Key", reader))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(host.Client, HttpMethod.Get, "/scoped", ("X-Api-Key", other))).StatusCode);
    }

    [Fact]
    public async Task KeyRateLimitPolicy_LimitsEachKey()
    {
        await using var host = await AuthTestSite.StartAsync(c =>
            c.Security.RateLimits.Policies.Add(new RateLimitPolicyConfig { Name = "per-key", Permits = 2, WindowSeconds = 3600 }));
        var limited = await CreateKeyAsync(host, new WebApiKeyRequest { Name = "limited", RateLimitPolicy = "per-key" });
        var free = await CreateKeyAsync(host, new WebApiKeyRequest { Name = "free" });

        await SendAsync(host.Client, HttpMethod.Get, "/me", ("X-Api-Key", limited));
        await SendAsync(host.Client, HttpMethod.Get, "/me", ("X-Api-Key", limited));
        Assert.Equal(HttpStatusCode.TooManyRequests, (await SendAsync(host.Client, HttpMethod.Get, "/me", ("X-Api-Key", limited))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(host.Client, HttpMethod.Get, "/me", ("X-Api-Key", free))).StatusCode);
    }

    [Fact]
    public async Task BearerTokens_UseTheAppValidator()
    {
        await using var host = await AuthTestSite.StartAsync(web: w => w.AddBearerTokens<TestTokenValidator>());

        var ok = await SendAsync(host.Client, HttpMethod.Get, "/me", ("Authorization", "Bearer good-token"));
        Assert.Equal("bearer", JsonDocument.Parse(await ok.Content.ReadAsStringAsync()).RootElement.GetProperty("scheme").GetString());

        var bad = await SendAsync(host.Client, HttpMethod.Get, "/me", ("Authorization", "Bearer bad-token"));
        Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
        Assert.Contains("invalid_token", bad.Headers.WwwAuthenticate.ToString());
    }

    [Fact]
    public async Task TrustedHeader_RequiresTrustedProxyAndSecret()
    {
        static void Trusted(WebLogicConfig c, bool trustLoopback)
        {
            c.Auth.TrustedHeader.Enabled = true;
            c.Auth.TrustedHeader.Secret = "proxy-secret";
            if (trustLoopback)
                c.Security.ForwardedHeaders.TrustedProxies.Add("127.0.0.1");
        }

        await using (var host = await AuthTestSite.StartAsync(c => Trusted(c, true)))
        {
            var ok = await SendAsync(host.Client, HttpMethod.Get, "/me", ("X-Forwarded-User", "u-42"), ("X-Forwarded-Groups", "staff, ops"), ("X-Proxy-Secret", "proxy-secret"));
            var me = JsonDocument.Parse(await ok.Content.ReadAsStringAsync()).RootElement;
            Assert.Equal("u-42", me.GetProperty("user").GetString());
            Assert.Equal("staff,ops", me.GetProperty("groups").GetString());

            Assert.Equal(HttpStatusCode.Unauthorized,
                (await SendAsync(host.Client, HttpMethod.Get, "/me", ("X-Forwarded-User", "u-42"), ("X-Proxy-Secret", "wrong"))).StatusCode);
        }

        await using (var host = await AuthTestSite.StartAsync(c => Trusted(c, false)))
        {
            // Not from a trusted proxy: the header is ignored, the request stays anonymous.
            Assert.Equal(HttpStatusCode.Unauthorized,
                (await SendAsync(host.Client, HttpMethod.Get, "/me", ("X-Forwarded-User", "admin"), ("X-Proxy-Secret", "proxy-secret"))).StatusCode);
        }
    }

    [Fact]
    public async Task InMemoryAccounts_AreRefusedInProduction_NullStoresAreFine()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => AuthTestSite.StartAsync(
            options: new WebLogicTestHostOptions { EnvironmentName = "Production", AllowDevelopmentStores = false }));
        Assert.Contains("IWebUserStore", error.Message);
        Assert.Contains("IWebSecurityNotifier", error.Message);

        // An app without accounts only needs a real session store.
        await using var host = await CL.WebLogic.IntegrationTests.Security.SecurityTestSite.StartAsync(
            web: w => w.UseSessionStore<TestSessionStore>(),
            options: new WebLogicTestHostOptions { EnvironmentName = "Production", AllowDevelopmentStores = false });
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/public")).StatusCode);
    }

    private sealed class TestTokenValidator : IWebBearerTokenValidator
    {
        public ValueTask<WebIdentity?> ValidateAsync(string token, WebSecurityContext context, CancellationToken cancellationToken) =>
            ValueTask.FromResult(token == "good-token"
                ? new WebIdentity { UserId = "svc", Scheme = WebAuthenticationSchemes.Bearer, Scopes = ["orders:read"] }
                : null);
    }

    private sealed class TestSessionStore : CL.WebLogic.Security.Sessions.IWebSessionStore
    {
        private readonly CL.WebLogic.Security.Sessions.InMemoryWebSessionStore _inner = new();

        public Task<CL.WebLogic.Security.Sessions.WebSessionRecord?> GetAsync(string idHash, CancellationToken cancellationToken) => _inner.GetAsync(idHash, cancellationToken);

        public Task SaveAsync(CL.WebLogic.Security.Sessions.WebSessionRecord record, CancellationToken cancellationToken) => _inner.SaveAsync(record, cancellationToken);

        public Task DeleteAsync(string idHash, CancellationToken cancellationToken) => _inner.DeleteAsync(idHash, cancellationToken);

        public Task<int> DeleteForUserAsync(string userId, CancellationToken cancellationToken) => _inner.DeleteForUserAsync(userId, cancellationToken);

        public Task<int> DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken) => _inner.DeleteExpiredAsync(now, cancellationToken);
    }
}

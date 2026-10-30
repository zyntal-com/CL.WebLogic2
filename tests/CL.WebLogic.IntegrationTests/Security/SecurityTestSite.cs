using System.Net.Http.Json;
using System.Text.Json;
using CL.WebLogic.Configuration.Models;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Results;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Security.Sessions;
using CL.WebLogic.Testing.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CL.WebLogic.IntegrationTests.Security;

/// <summary>A host with routes covering every security stage.</summary>
internal static class SecurityTestSite
{
    public static Task<WebLogicTestHost> StartAsync(Action<WebLogicConfig>? configure = null, Action<WebLogicBuilder>? web = null, WebLogicTestHostOptions? options = null)
    {
        var config = options?.Config ?? new WebLogicConfig();
        configure?.Invoke(config);
        options ??= new WebLogicTestHostOptions();
        options.Config = config;
        options.ConfigureWebLogic = builder =>
        {
            builder.AddContributor(new DelegateContributor(Routes));
            builder.AddAuthorizationPolicy("editors", p => p.RequireAnyGroup("editor"));
            web?.Invoke(builder);
        };
        return WebLogicTestHost.StartAsync(options);
    }

    private static ValueTask<WebResult> Text(string text) => ValueTask.FromResult<WebResult>(WebResult.Text(text));

    private static void Routes(WebContributionBuilder b)
    {
        b.MapPage("/public", (_, _) => Text("public")).AllowAnonymous();
        b.MapPage("/secure", (r, _) => Text("secure:" + r.Identity.UserId));
        b.MapApi("/api/secure", (r, _) => Text("api:" + r.Identity.UserId));
        b.MapPage("/admin", (_, _) => Text("admin")).RequireGroups("admin");
        b.MapApi("/perm", (_, _) => Text("perm")).RequirePermissions("posts.edit");
        b.MapApi("/policy", (_, _) => Text("policy")).RequirePolicy("editors");
        b.MapApi("/bad-policy", (_, _) => Text("bad")).RequirePolicy("does-not-exist");
        b.MapPage("/cached-form", (r, _) =>
        {
            r.GetRequiredService<CL.WebLogic.Security.Antiforgery.WebCsrfTokens>().GetToken(r.HttpContext);
            return ValueTask.FromResult(WebResult.Text("form").WithHeader("Cache-Control", "public, max-age=600"));
        }).AllowAnonymous();
        b.MapGroup("/open").AllowAnonymous().MapApi("/policy", (_, _) => Text("open-policy")).RequirePolicy("editors");

        b.MapPage("/signin", async (r, ct) =>
        {
            var manager = r.GetRequiredService<WebSessionManager>();
            await manager.SignInAsync(r.Security, new WebSignIn
            {
                UserId = r.GetQuery("user") ?? "u1",
                Groups = Split(r.GetQuery("groups")),
                Permissions = Split(r.GetQuery("perms"))
            }, ct);
            return WebResult.Text("signed-in");
        }).AllowAnonymous();
        b.MapPage("/signout", async (r, ct) =>
        {
            await r.GetRequiredService<WebSessionManager>().SignOutAsync(r.Security, r.GetQuery("everywhere") == "1", ct);
            return WebResult.Text("signed-out");
        }).AllowAnonymous();

        b.MapApi("/session/set", (r, _) =>
        {
            r.Session.SetString("v", r.GetQuery("v") ?? "");
            return Text("set");
        }).AllowAnonymous();
        b.MapApi("/session/get", (r, _) => Text(r.Session.GetString("v") ?? "none")).AllowAnonymous();

        b.MapApi("/submit", (_, _) => Text("submitted"), "POST").AllowAnonymous();
        b.MapApi("/hook", (_, _) => Text("hooked"), "POST").AllowAnonymous().AllowCrossSitePost();
        b.MapApi("/limited", (_, _) => Text("ok")).AllowAnonymous().WithRateLimit("tiny");
        b.MapApi("/unknown-limit", (_, _) => Text("ok")).AllowAnonymous().WithRateLimit("nope");

        b.MapPage("/handler-csp", (_, _) =>
            ValueTask.FromResult(WebResult.Text("x").WithHeader("Content-Security-Policy", "default-src *"))).AllowAnonymous();
        b.MapPage("/framable", (r, _) =>
        {
            r.Security.OverrideHeader("X-Frame-Options", "SAMEORIGIN");
            return Text("framable");
        }).AllowAnonymous();
        b.MapApi("/whoami", (r, _) => Text(r.ClientIp + "|" + r.HttpContext.Request.Scheme + "|" + r.HttpContext.Request.Host + "|" + r.HttpContext.Request.PathBase)).AllowAnonymous();

        b.MapFallback((_, _) => Text("fallback"), "GET", "POST").AllowAnonymous();
    }

    private static string[] Split(string? value) =>
        string.IsNullOrEmpty(value) ? [] : value.Split(',', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Fetches a CSRF token (the client keeps the pre-session cookie).</summary>
    public static async Task<string> GetCsrfTokenAsync(HttpClient client)
    {
        var json = await client.GetFromJsonAsync<JsonElement>("/_weblogic/csrf");
        return json.GetProperty("token").GetString()!;
    }

    /// <summary>POSTs with the CSRF header.</summary>
    public static Task<HttpResponseMessage> PostWithTokenAsync(HttpClient client, string path, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = new StringContent("{}") };
        request.Headers.Add("X-CSRF-Token", token);
        return client.SendAsync(request);
    }
}

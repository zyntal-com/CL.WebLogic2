using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using CL.WebLogic.Results;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Security.Webhooks;
using CL.WebLogic.Testing.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CL.WebLogic.IntegrationTests.Security;

public sealed class WebhookTests
{
    private const string OldSecret = "whsec_old";
    private const string NewSecret = "whsec_new";

    [Fact]
    public async Task ValidSignature_IsAccepted_AndBodyCanBeReadAgain()
    {
        await using var host = await StartAsync(new ManualTimeProvider(DateTimeOffset.UtcNow));
        const string body = """{"event":"paid","amount":12}""";

        var response = await PostAsync(host, "/hooks/github", body, ("X-Hub-Signature-256", "sha256=" + Hex(OldSecret, body)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ok paid", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task TamperedBody_IsRefused()
    {
        await using var host = await StartAsync(new ManualTimeProvider(DateTimeOffset.UtcNow));

        var response = await PostAsync(host, "/hooks/github", """{"event":"paid","amount":9999}""",
            ("X-Hub-Signature-256", "sha256=" + Hex(OldSecret, """{"event":"paid","amount":12}""")));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("weblogic.webhook.bad_signature", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task MissingSignature_IsRefused()
    {
        await using var host = await StartAsync(new ManualTimeProvider(DateTimeOffset.UtcNow));

        var response = await PostAsync(host, "/hooks/github", """{"event":"paid"}""");

        Assert.Equal("weblogic.webhook.missing_signature", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task RotatedSecond_Secret_IsAccepted()
    {
        await using var host = await StartAsync(new ManualTimeProvider(DateTimeOffset.UtcNow));
        const string body = """{"event":"refund"}""";

        var response = await PostAsync(host, "/hooks/github", body, ("X-Hub-Signature-256", "sha256=" + Hex(NewSecret, body)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task StripeStyle_FreshTimestamp_IsAccepted_OldOneRefused()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        await using var host = await StartAsync(clock);
        const string body = """{"event":"charge"}""";
        var now = clock.GetUtcNow().ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var header = $"t={now},v1={new string('0', 64)},v1={Hex(NewSecret, now + "." + body)}";

        var fresh = await PostAsync(host, "/hooks/stripe", body, ("Stripe-Signature", header));
        Assert.Equal(HttpStatusCode.OK, fresh.StatusCode);

        clock.Advance(TimeSpan.FromMinutes(6));
        var replayed = await PostAsync(host, "/hooks/stripe", body, ("Stripe-Signature", header));
        Assert.Equal(HttpStatusCode.Unauthorized, replayed.StatusCode);
        Assert.Equal("weblogic.webhook.expired", await replayed.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task StripeStyle_ChangedTimestamp_IsRefused()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        await using var host = await StartAsync(clock);
        const string body = """{"event":"charge"}""";
        var signedAt = clock.GetUtcNow().AddMinutes(-1).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var now = clock.GetUtcNow().ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

        var response = await PostAsync(host, "/hooks/stripe", body, ("Stripe-Signature", $"t={now},v1={Hex(OldSecret, signedAt + "." + body)}"));

        Assert.Equal("weblogic.webhook.bad_signature", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task OversizedBody_IsRefused()
    {
        await using var host = await StartAsync(new ManualTimeProvider(DateTimeOffset.UtcNow));
        var body = new string('x', 2048);

        var response = await PostAsync(host, "/hooks/github", body, ("X-Hub-Signature-256", "sha256=" + Hex(OldSecret, body)));

        Assert.Equal("weblogic.webhook.too_large", await response.Content.ReadAsStringAsync());
    }

    private static Task<WebLogicTestHost> StartAsync(ManualTimeProvider clock) =>
        WebLogicTestHost.StartAsync(new WebLogicTestHostOptions
        {
            ConfigureServices = s => s.Replace(ServiceDescriptor.Singleton<TimeProvider>(clock)),
            ConfigureWebLogic = web => web.AddContributor(new DelegateContributor(builder =>
            {
                builder.MapApi("/hooks/github", async (request, ct) =>
                {
                    var verifier = request.GetRequiredService<WebWebhookVerifier>();
                    var options = WebWebhookOptions.GitHub(OldSecret, NewSecret);
                    var verified = await verifier.VerifyAsync(request, new WebWebhookOptions
                    {
                        Secrets = options.Secrets,
                        SignatureHeader = options.SignatureHeader,
                        SignaturePrefix = options.SignaturePrefix,
                        MaxBodyBytes = 1024
                    }, ct);
                    if (verified.IsFailure)
                        return WebResult.Text(verified.Error!.Code, 401);
                    var payload = await request.ReadJsonAsync<Dictionary<string, object>>(ct);
                    return WebResult.Text("ok " + payload!["event"]);
                }, "POST").AllowAnonymous().AllowCrossSitePost();

                builder.MapApi("/hooks/stripe", async (request, ct) =>
                {
                    var verified = await request.GetRequiredService<WebWebhookVerifier>().VerifyAsync(request, WebWebhookOptions.Stripe(OldSecret, NewSecret), ct);
                    return verified.IsFailure ? WebResult.Text(verified.Error!.Code, 401) : WebResult.Text("ok");
                }, "POST").AllowAnonymous().AllowCrossSitePost();
            }))
        });

    private static async Task<HttpResponseMessage> PostAsync(WebLogicTestHost host, string path, string body, params (string Name, string Value)[] headers)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        foreach (var (name, value) in headers)
            request.Headers.TryAddWithoutValidation(name, value);
        return await host.Client.SendAsync(request);
    }

    private static string Hex(string secret, string payload) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload)));
}

using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CL.WebLogic.IntegrationTests.Forms;

public sealed partial class FormFlashTests
{
    [Fact]
    public async Task Plain_success_carries_the_message_to_the_next_page_once()
    {
        await using var host = await FormsTestSite.StartAsync();
        var page = await host.Client.GetStringAsync("/contact");

        var ok = await host.Client.PostAsync("/contact/quick", FormsTestSite.Post(page, ("Name", "Ada"), ("Email", "ada@example.com")));
        Assert.Equal(HttpStatusCode.SeeOther, ok.StatusCode);
        Assert.Contains(ok.Headers.GetValues("Set-Cookie"), c => c.StartsWith("wl_flash=", StringComparison.Ordinal) && c.Contains("httponly", StringComparison.OrdinalIgnoreCase));

        var flash = Flash(await host.Client.GetStringAsync("/thanks"));
        Assert.Equal("Saved.", flash.GetProperty("message").GetString());
        Assert.Equal("success", flash.GetProperty("variant").GetString());

        Assert.Equal(JsonValueKind.Null, Flash(await host.Client.GetStringAsync("/thanks")).ValueKind);
    }

    [Fact]
    public async Task Plain_failure_without_fallback_goes_back_with_the_errors()
    {
        await using var host = await FormsTestSite.StartAsync();
        var page = await host.Client.GetStringAsync("/contact");

        using var post = new HttpRequestMessage(HttpMethod.Post, "/contact/quick") { Content = FormsTestSite.Post(page, ("Name", "Ada"), ("Email", "bad")) };
        post.Headers.Referrer = new Uri(host.Client.BaseAddress!, "/thanks?from=form");
        var invalid = await host.Client.SendAsync(post);
        Assert.Equal(HttpStatusCode.SeeOther, invalid.StatusCode);
        Assert.Equal("/thanks?from=form", invalid.Headers.Location!.OriginalString);

        var flash = Flash(await host.Client.GetStringAsync("/thanks"));
        Assert.Equal("Check the form.", flash.GetProperty("message").GetString());
        Assert.Equal("danger", flash.GetProperty("variant").GetString());
    }

    [Fact]
    public async Task Plain_failure_without_a_referer_stays_a_422()
    {
        // A Referer from another site never gets this far: the CSRF origin check refuses the post (403).
        await using var host = await FormsTestSite.StartAsync();
        var page = await host.Client.GetStringAsync("/contact");

        var invalid = await host.Client.PostAsync("/contact/quick", FormsTestSite.Post(page, ("Name", "Ada"), ("Email", "bad")));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
        Assert.Equal("Check the form.", await invalid.Content.ReadAsStringAsync());
        Assert.False(invalid.Headers.TryGetValues("Set-Cookie", out var cookies) && cookies.Any(c => c.StartsWith("wl_flash=", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Forged_flash_cookies_are_dropped()
    {
        await using var host = await FormsTestSite.StartAsync();
        using var get = new HttpRequestMessage(HttpMethod.Get, "/thanks");
        get.Headers.Add("Cookie", "wl_flash=not-protected");
        var response = await host.Client.SendAsync(get);

        Assert.Equal(JsonValueKind.Null, Flash(await response.Content.ReadAsStringAsync()).ValueKind);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith("wl_flash=;", StringComparison.Ordinal));
    }

    private static JsonElement Flash(string html) =>
        JsonDocument.Parse(Config().Match(html).Groups[1].Value).RootElement.GetProperty("flash").Clone();

    [GeneratedRegex("<script type=\"application/json\" id=\"wl-config\">(.*?)</script>", RegexOptions.Singleline)]
    private static partial Regex Config();
}

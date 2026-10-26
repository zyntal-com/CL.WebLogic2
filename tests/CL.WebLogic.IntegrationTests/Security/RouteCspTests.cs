using CL.WebLogic.Results;
using CL.WebLogic.Routing.Models;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Testing.Services;

namespace CL.WebLogic.IntegrationTests.Security;

/// <summary>Per-route Content-Security-Policy additions and the Reporting API header.</summary>
public sealed class RouteCspTests
{
    [Fact]
    public async Task Only_the_route_that_embeds_gets_the_extra_frame_source()
    {
        await using var host = await SecurityTestSite.StartAsync(c => c.Security.Headers.CspReportTo = "https://reports.test/csp", web => web.AddContributor(new DelegateContributor(b =>
        {
            var media = b.MapGroup("/media").AllowAnonymous().WithCsp(c => c.AddMediaSource("https://cdn.test"));
            media.MapPage("/video", (_, _) => ValueTask.FromResult<WebResult>(WebResult.Html("<p>video</p>")))
                .WithCsp(c => c.AddFrameSource("https://www.youtube-nocookie.com"));
            b.MapPage("/plain", (_, _) => ValueTask.FromResult<WebResult>(WebResult.Html("<p>plain</p>"))).AllowAnonymous();
        }), new WebContributorDescriptor { Id = "tests.csp", Name = "CSP" }));

        var video = await host.Client.GetAsync("/media/video");
        var videoCsp = video.Headers.GetValues("Content-Security-Policy").Single();
        Assert.Contains("frame-src https://www.youtube-nocookie.com", videoCsp);
        Assert.Contains("media-src 'self' https://cdn.test", videoCsp); // inherited from the group
        Assert.Contains("report-to csp-endpoint", videoCsp);
        Assert.Equal("csp-endpoint=\"https://reports.test/csp\"", video.Headers.GetValues("Reporting-Endpoints").Single());

        var plainCsp = (await host.Client.GetAsync("/plain")).Headers.GetValues("Content-Security-Policy").Single();
        Assert.Contains("frame-src 'none'", plainCsp);
        Assert.DoesNotContain("media-src", plainCsp);
    }
}

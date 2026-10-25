using CL.WebLogic.Caching.Services;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.IntegrationTests.Security;
using CL.WebLogic.Results;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Security.Antiforgery;
using CL.WebLogic.Testing.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CL.WebLogic.IntegrationTests.Caching;

public sealed class CachingTests
{
    private sealed class Counter
    {
        private int _value;

        public int Next() => Interlocked.Increment(ref _value);
    }

    private static Task<WebLogicTestHost> StartAsync(Counter counter) =>
        SecurityTestSite.StartAsync(web: web =>
        {
            web.AddOutputCachePolicy("posts", p => p.Tag("posts").Expire(TimeSpan.FromMinutes(5)));
            web.AddContributor(new DelegateContributor(b =>
            {
                b.MapPage("/cached", (_, _) => ValueTask.FromResult<WebResult>(WebResult.Text("n=" + counter.Next()))).AllowAnonymous().WithOutputCache("default");
                b.MapPage("/tagged", (_, _) => ValueTask.FromResult<WebResult>(WebResult.Text("n=" + counter.Next()))).AllowAnonymous().WithOutputCache("posts");
                b.MapPage("/with-form", (r, _) =>
                {
                    r.GetRequiredService<IWebCsrfTokens>().GetToken(r.HttpContext);
                    return ValueTask.FromResult<WebResult>(WebResult.Text("n=" + counter.Next()));
                }).AllowAnonymous().WithOutputCache("default");
                b.MapPage("/fragment", async (r, ct) =>
                    WebResult.Text(await r.Cache.GetOrAddAsync("menu", _ => ValueTask.FromResult("n=" + counter.Next()), tags: ["menu"], cancellationToken: ct))).AllowAnonymous();
            }), new Routing.Models.WebContributorDescriptor { Id = "tests.cache", Name = "Cache" });
        });

    [Fact]
    public async Task Anonymous_pages_are_served_from_the_output_cache()
    {
        var counter = new Counter();
        await using var host = await StartAsync(counter);

        Assert.Equal("n=1", await host.Client.GetStringAsync("/cached"));
        Assert.Equal("n=1", await host.CreateClient().GetStringAsync("/cached"));
    }

    [Fact]
    public async Task Signed_in_requests_and_csrf_pages_are_never_cached()
    {
        var counter = new Counter();
        await using var host = await StartAsync(counter);
        await host.Client.GetAsync("/signin?user=u1");

        Assert.Equal("n=1", await host.Client.GetStringAsync("/cached"));
        Assert.Equal("n=2", await host.Client.GetStringAsync("/cached"));
        var anonymous = host.CreateClient();
        Assert.Equal("n=3", await anonymous.GetStringAsync("/cached")); // the signed-in answer was not stored

        Assert.Equal("n=4", await anonymous.GetStringAsync("/with-form"));
        Assert.Equal("n=5", await anonymous.GetStringAsync("/with-form")); // a page that issued a CSRF token is personal
    }

    [Fact]
    public async Task Visitors_with_an_anonymous_session_bypass_the_output_cache()
    {
        // Security review: a page reading anonymous session data (cart, preferences) could be
        // stored for one visitor and served to everyone else.
        var counter = new Counter();
        await using var host = await StartAsync(counter);
        Assert.Equal("n=1", await host.Client.GetStringAsync("/cached"));

        var shopper = host.CreateClient();
        await shopper.GetAsync("/session/set?v=cart");
        Assert.Equal("n=2", await shopper.GetStringAsync("/cached"));
        Assert.Equal("n=1", await host.CreateClient().GetStringAsync("/cached")); // the shared entry is untouched
    }

    [Fact]
    public async Task Tags_evict_pages_and_fragments()
    {
        var counter = new Counter();
        await using var host = await StartAsync(counter);
        var invalidator = host.Services.GetRequiredService<IWebCacheInvalidator>();

        Assert.Equal("n=1", await host.Client.GetStringAsync("/tagged"));
        Assert.Equal("n=1", await host.Client.GetStringAsync("/tagged"));
        Assert.Equal("n=2", await host.Client.GetStringAsync("/fragment"));
        Assert.Equal("n=2", await host.Client.GetStringAsync("/fragment"));

        await invalidator.EvictTagsAsync(["posts", "menu"], CancellationToken.None);

        Assert.Equal("n=3", await host.Client.GetStringAsync("/tagged"));
        Assert.Equal("n=4", await host.Client.GetStringAsync("/fragment"));
    }
}

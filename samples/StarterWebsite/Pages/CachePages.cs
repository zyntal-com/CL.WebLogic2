using CL.WebLogic.Caching.Services;
using CL.WebLogic.Results;
using CL.WebLogic.Routing;
using CL.WebLogic.Routing.Services;

namespace StarterWebsite.Pages;

/// <summary>
/// Output and fragment caching: <c>/cache</c> is served from the output cache for anonymous
/// visitors (signed-in visitors always get a fresh page), with a slow fragment cached by tag.
/// The evict button posts through the client (which fetches a CSRF token on demand, so the
/// cached page itself never contains one).
/// </summary>
public sealed class CachePages : IWebContributor
{
    /// <summary>The output cache policy of the demo page.</summary>
    public const string Policy = "cache-demo";

    /// <summary>The tag evicting both the page and the fragment.</summary>
    public const string Tag = "cache-demo";

    private readonly IWebCacheInvalidator _invalidator;
    private readonly TimeProvider _time;

    /// <summary>Creates the contributor.</summary>
    public CachePages(IWebCacheInvalidator invalidator, TimeProvider time)
    {
        _invalidator = invalidator;
        _time = time;
    }

    /// <inheritdoc/>
    public Task ContributeAsync(WebContributionBuilder builder, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.MapPage("/cache", async (r, ct) =>
        {
            var fragment = await r.Cache.GetOrAddAsync("cache-demo:report", async token =>
            {
                await Task.Delay(400, token).ConfigureAwait(false); // pretend this is expensive
                return $"Report built at {_time.GetUtcNow():HH:mm:ss} UTC";
            }, TimeSpan.FromMinutes(5), [Tag], ct).ConfigureAwait(false);
            return WebResult.Page("templates/cache", new { RenderedAt = _time.GetUtcNow().ToString("HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture), Fragment = fragment },
                PageModels.Meta("Caching"));
        }).AllowAnonymous().WithOutputCache(Policy);
        builder.MapApi("/cache/evict", async (_, ct) =>
        {
            await _invalidator.EvictTagsAsync([Tag], ct).ConfigureAwait(false);
            return WebResult.Status(204);
        }, "POST").AllowAnonymous().WithRateLimit("forms");
        return Task.CompletedTask;
    }
}

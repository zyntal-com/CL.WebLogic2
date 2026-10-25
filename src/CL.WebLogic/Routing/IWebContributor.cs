using CL.WebLogic.Routing.Services;

namespace CL.WebLogic.Routing;

/// <summary>
/// Contributes routes (and later widgets, form providers, realtime mappings) to WebLogic.
/// <para>
/// Applications register contributors with <c>web.AddContributor&lt;T&gt;()</c>; CodeLogic plugins
/// that implement this interface are registered automatically when loaded and removed again
/// when unloaded.
/// </para>
/// </summary>
public interface IWebContributor
{
    /// <summary>Registers this contributor's routes on <paramref name="builder"/>.</summary>
    Task ContributeAsync(WebContributionBuilder builder, CancellationToken cancellationToken);
}

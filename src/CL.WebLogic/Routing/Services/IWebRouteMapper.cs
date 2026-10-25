using CL.WebLogic.Routing.Models;

namespace CL.WebLogic.Routing.Services;

/// <summary>
/// Maps routes. Implemented by <see cref="WebContributionBuilder"/> (root) and
/// <see cref="WebRouteGroupBuilder"/> (prefixed groups). The typed helpers
/// (<c>MapPage</c>, <c>MapApi</c>, …) live in <see cref="WebRouteMapperExtensions"/>.
/// </summary>
public interface IWebRouteMapper
{
    /// <summary>Stages a route of <paramref name="kind"/> at <paramref name="pattern"/> (relative to the group prefix).</summary>
    WebRouteBuilder Map(WebRouteKind kind, string pattern, WebRouteHandler handler, params string[] methods);

    /// <summary>Creates a nested group under <paramref name="prefix"/>; options on the group apply to every route in it.</summary>
    WebRouteGroupBuilder MapGroup(string prefix);
}

using CL.WebLogic.Security.Authorization;
using CL.WebLogic.Security.Pipeline;
using CL.WebLogic.Widgets.Models;

namespace CL.WebLogic.Widgets.Services;

/// <summary>
/// The placement rule shared by the widget endpoint and dashboards: a widget whose every placement
/// has an access rule is only for callers one of those rules allows, wherever it is used. Otherwise
/// a stricter placement could be skipped by calling the widget directly or adding it to a dashboard.
/// </summary>
internal static class WebWidgetPlacementAccess
{
    /// <summary>True when the caller may use <paramref name="widget"/> outside its placements.</summary>
    public static async ValueTask<bool> AllowsAsync(WebWidgetRegistry registry, WebAccessEvaluator access, WebSecurityContext? security,
        WebWidgetDefinition widget, CancellationToken cancellationToken)
    {
        var placements = registry.Placements.Where(p => string.Equals(p.WidgetName, widget.Name, StringComparison.OrdinalIgnoreCase)).ToList();
        if (placements.Count == 0 || placements.Any(p => p.Access is null))
            return true;
        foreach (var placement in placements)
        {
            if (await access.AllowsAsync(security, placement.Access!, cancellationToken).ConfigureAwait(false))
                return true;
        }
        return false;
    }

    /// <summary>True when <paramref name="instanceId"/> is the id of a placement (dashboards may not reuse it).</summary>
    public static bool IsPlacementInstance(WebWidgetRegistry registry, string instanceId) =>
        instanceId.Length > 0 && registry.Placements.Any(p => string.Equals(p.InstanceId, instanceId, StringComparison.Ordinal));
}

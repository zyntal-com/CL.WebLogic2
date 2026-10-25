using CL.WebLogic.Routing.Models;
using CL.WebLogic.Security.Authorization;

namespace CL.WebLogic.Widgets.Models;

/// <summary>A widget placed in a widget area (<c>{widgetarea:sidebar}</c>).</summary>
public sealed class WebWidgetPlacement
{
    /// <summary>Area name.</summary>
    public required string Area { get; init; }

    /// <summary>Widget name.</summary>
    public required string WidgetName { get; init; }

    /// <summary>Instance id (settings key).</summary>
    public string? InstanceId { get; init; }

    /// <summary>Who registered it.</summary>
    public required WebContributorDescriptor Contributor { get; init; }

    /// <summary>Sort order within the area.</summary>
    public int Order { get; init; }

    /// <summary>Parameters passed to the widget.</summary>
    public IReadOnlyDictionary<string, string> Parameters { get; init; } = new Dictionary<string, string>();

    /// <summary>Only on paths matching one of these globs (empty = all).</summary>
    public IReadOnlyList<string> IncludePaths { get; init; } = [];

    /// <summary>Never on paths matching these globs.</summary>
    public IReadOnlyList<string> ExcludePaths { get; init; } = [];

    /// <summary>Extra access rule for this placement (on top of the widget's own).</summary>
    public WebAccessRule? Access { get; init; }
}

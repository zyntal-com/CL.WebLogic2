using CL.WebLogic.Security.Authorization;

namespace CL.WebLogic.Widgets.Models;

/// <summary>Options of a widget placement.</summary>
public sealed class WebWidgetPlacementOptions
{
    /// <summary>Instance id (settings key).</summary>
    public string? InstanceId { get; set; }

    /// <summary>Sort order.</summary>
    public int Order { get; set; }

    /// <summary>Parameters.</summary>
    public Dictionary<string, string> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Include path globs.</summary>
    public List<string> IncludePaths { get; } = [];

    /// <summary>Exclude path globs.</summary>
    public List<string> ExcludePaths { get; } = [];

    /// <summary>Extra access rule.</summary>
    public WebAccessRule? Access { get; set; }
}

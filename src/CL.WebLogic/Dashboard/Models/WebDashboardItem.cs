namespace CL.WebLogic.Dashboard.Models;

/// <summary>A widget on a dashboard.</summary>
public sealed record WebDashboardItem
{
    /// <summary>Instance id (generated when empty; widget settings are stored under it).</summary>
    public string InstanceId { get; init; } = string.Empty;

    /// <summary>Widget name.</summary>
    public required string WidgetName { get; init; }

    /// <summary>Zone (column/region), e.g. <c>main</c> or <c>side</c>.</summary>
    public string Zone { get; init; } = "main";

    /// <summary>Position within the zone.</summary>
    public int Order { get; init; }

    /// <summary>Widget parameters.</summary>
    public IReadOnlyDictionary<string, string> Parameters { get; init; } = new Dictionary<string, string>();
}

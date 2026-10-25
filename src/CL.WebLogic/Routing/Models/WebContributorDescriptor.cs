namespace CL.WebLogic.Routing.Models;

/// <summary>Identifies who registered a set of routes, widgets and providers.</summary>
public sealed class WebContributorDescriptor
{
    /// <summary>Stable unique id (for plugins: the plugin id).</summary>
    public required string Id { get; init; }

    /// <summary>Human-readable name shown in the explorer.</summary>
    public required string Name { get; init; }

    /// <summary>Kind label, e.g. <c>Application</c>, <c>Plugin</c>, <c>System</c>.</summary>
    public string Kind { get; init; } = "Contributor";

    /// <summary>Optional description shown in the explorer.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Builds a descriptor from a contributor type name.</summary>
    public static WebContributorDescriptor FromType(Type type, string kind = "Contributor") => new()
    {
        Id = type.FullName ?? type.Name,
        Name = type.Name,
        Kind = kind
    };
}

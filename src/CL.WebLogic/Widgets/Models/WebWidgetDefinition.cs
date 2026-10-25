using CL.WebLogic.Routing.Models;
using CL.WebLogic.Security.Authorization;

namespace CL.WebLogic.Widgets.Models;

/// <summary>A registered widget.</summary>
public sealed class WebWidgetDefinition
{
    /// <summary>Name (<c>^[a-z0-9][a-z0-9._-]*$</c>).</summary>
    public required string Name { get; init; }

    /// <summary>Who registered it.</summary>
    public required WebContributorDescriptor Contributor { get; init; }

    /// <summary>Renderer.</summary>
    public required WebWidgetHandler Handler { get; init; }

    /// <summary>Description.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Tags.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Access.</summary>
    public WebAccessRule Access { get; init; } = WebAccessRule.Authenticated;

    /// <summary>Example parameters.</summary>
    public IReadOnlyDictionary<string, string> SampleParameters { get; init; } = new Dictionary<string, string>();

    /// <summary>Client refresh interval in seconds (0 = none).</summary>
    public int RefreshSeconds { get; init; }

    /// <summary>Allowed on dashboards.</summary>
    public bool Dashboard { get; init; } = true;

    /// <summary>Actions.</summary>
    public IReadOnlyDictionary<string, WebWidgetActionHandler> Actions { get; init; } = new Dictionary<string, WebWidgetActionHandler>();
}

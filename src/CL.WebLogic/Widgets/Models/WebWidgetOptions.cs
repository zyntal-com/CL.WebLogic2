using CL.WebLogic.Security.Authorization;

namespace CL.WebLogic.Widgets.Models;

/// <summary>Options of a widget.</summary>
public sealed class WebWidgetOptions
{
    /// <summary>Description (explorer, dashboard pickers).</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Tags.</summary>
    public List<string> Tags { get; } = [];

    /// <summary>Who may see and use the widget (signed-in users by default).</summary>
    public WebAccessRule Access { get; set; } = WebAccessRule.Authenticated;

    /// <summary>Example parameters (explorer, dashboard pickers).</summary>
    public Dictionary<string, string> SampleParameters { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Client refresh interval in seconds (0 = none; minimum 5).</summary>
    public int RefreshSeconds { get; set; }

    /// <summary>Allowed on dashboards.</summary>
    public bool Dashboard { get; set; } = true;

    /// <summary>Actions by name.</summary>
    public Dictionary<string, WebWidgetActionHandler> Actions { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Allows anonymous callers.</summary>
    public WebWidgetOptions AllowAnonymous()
    {
        Access = WebAccessRule.Anonymous;
        return this;
    }

    /// <summary>Adds an action.</summary>
    public WebWidgetOptions Action(string name, WebWidgetActionHandler handler)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        Actions[name] = handler ?? throw new ArgumentNullException(nameof(handler));
        return this;
    }
}

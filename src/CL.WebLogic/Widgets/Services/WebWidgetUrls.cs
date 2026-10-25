using System.Text;

namespace CL.WebLogic.Widgets.Services;

/// <summary>URLs of the widget endpoints (for templates and widget markup).</summary>
public static class WebWidgetUrls
{
    /// <summary>The prefix of the widget endpoints.</summary>
    public const string Prefix = "/_weblogic/widgets/";

    /// <summary>Re-render URL (<c>data-wl-refresh</c>).</summary>
    public static string Refresh(string basePath, string widget, string? instanceId, IReadOnlyDictionary<string, string>? parameters = null)
    {
        var url = new StringBuilder(basePath).Append(Prefix).Append(Uri.EscapeDataString(widget));
        var separator = '?';
        if (instanceId is not null)
        {
            url.Append(separator).Append("instance=").Append(Uri.EscapeDataString(instanceId));
            separator = '&';
        }
        foreach (var (key, value) in parameters ?? new Dictionary<string, string>())
        {
            if (string.Equals(key, "instance", StringComparison.OrdinalIgnoreCase))
                continue;
            url.Append(separator).Append(Uri.EscapeDataString(key)).Append('=').Append(Uri.EscapeDataString(value));
            separator = '&';
        }
        return url.ToString();
    }

    /// <summary>Action URL (<c>data-wl-action</c>).</summary>
    public static string Action(string basePath, string widget, string action, string? instanceId) =>
        basePath + Prefix + Uri.EscapeDataString(widget) + "/actions/" + Uri.EscapeDataString(action)
        + (instanceId is null ? "" : "?instance=" + Uri.EscapeDataString(instanceId));
}

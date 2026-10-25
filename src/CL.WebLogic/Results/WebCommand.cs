using CL.WebLogic.Runtime.Services;

namespace CL.WebLogic.Results;

/// <summary>
/// A client-side command executed by the WebLogic JS client. Only an allowlisted set of
/// command types exists, on the server and in the client — there is no arbitrary script.
/// </summary>
public sealed class WebCommand
{
    private WebCommand(string type, IReadOnlyDictionary<string, object?> args)
    {
        Type = type;
        Args = args;
    }

    /// <summary>The command type (e.g. <c>toast</c>).</summary>
    public string Type { get; }

    /// <summary>Command arguments, serialized next to <c>type</c>.</summary>
    public IReadOnlyDictionary<string, object?> Args { get; }

    /// <summary>The JSON shape sent to the client: <c>{ "type": …, …args }</c>.</summary>
    public IReadOnlyDictionary<string, object?> ToPayload()
    {
        var payload = new Dictionary<string, object?>(Args, StringComparer.Ordinal) { ["type"] = Type };
        return payload;
    }

    /// <summary>Shows a toast. <paramref name="variant"/>: success, info, warning, danger.</summary>
    public static WebCommand Toast(string message, string variant = "success") =>
        new("toast", new Dictionary<string, object?> { ["message"] = message, ["variant"] = variant });

    /// <summary>Shows a blocking overlay message for <paramref name="durationMs"/>.</summary>
    public static WebCommand Overlay(string title, string message, string variant = "success", int durationMs = 2000) =>
        new("overlay", new Dictionary<string, object?> { ["title"] = title, ["message"] = message, ["variant"] = variant, ["duration"] = durationMs });

    /// <summary>Full page load of a local URL.</summary>
    public static WebCommand Redirect(string localUrl) => new("redirect", Local(localUrl));

    /// <summary>Client-side (PJAX) navigation to a local URL.</summary>
    public static WebCommand Navigate(string localUrl) => new("navigate", Local(localUrl));

    /// <summary>Reloads the current page.</summary>
    public static WebCommand Reload() => new("reload", new Dictionary<string, object?>());

    /// <summary>
    /// Replaces the element matching <paramref name="selector"/> with server-rendered HTML.
    /// The HTML must come from a template (encoded) — never from raw user input.
    /// </summary>
    public static WebCommand Replace(string selector, string html) =>
        new("replace", new Dictionary<string, object?> { ["selector"] = selector, ["html"] = html });

    /// <summary>Removes the element matching <paramref name="selector"/>.</summary>
    public static WebCommand Remove(string selector) =>
        new("remove", new Dictionary<string, object?> { ["selector"] = selector });

    /// <summary>Adds a CSS class to matching elements.</summary>
    public static WebCommand AddClass(string selector, string className) =>
        new("addClass", new Dictionary<string, object?> { ["selector"] = selector, ["className"] = className });

    /// <summary>Removes a CSS class from matching elements.</summary>
    public static WebCommand RemoveClass(string selector, string className) =>
        new("removeClass", new Dictionary<string, object?> { ["selector"] = selector, ["className"] = className });

    private static Dictionary<string, object?> Local(string url)
    {
        if (!WebUrl.IsLocal(url))
            throw new ArgumentException($"'{url}' is not a local URL.", nameof(url));
        return new Dictionary<string, object?> { ["url"] = url };
    }
}

using System.Text.Json;
using CL.WebLogic.Results;
using CL.WebLogic.Widgets.Services;

namespace CL.WebLogic.Widgets.Models;

/// <summary>Passed to a widget action.</summary>
public sealed class WebWidgetActionContext
{
    private const int MaxSettings = 50;
    private const int MaxSettingLength = 4000;
    private readonly WebWidgetRenderer _renderer;

    internal WebWidgetActionContext(string actionName, WebWidgetContext widget, JsonElement payload, WebWidgetRenderer renderer)
    {
        ActionName = actionName;
        Widget = widget;
        Payload = payload;
        _renderer = renderer;
    }

    /// <summary>Action name.</summary>
    public string ActionName { get; }

    /// <summary>The widget.</summary>
    public WebWidgetContext Widget { get; }

    /// <summary>The posted JSON payload (untrusted).</summary>
    public JsonElement Payload { get; }

    /// <summary>A string argument from the payload object.</summary>
    public string? GetArgument(string name) =>
        Payload.ValueKind == JsonValueKind.Object && Payload.TryGetProperty(name, out var value)
            ? value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString()
            : null;

    /// <summary>
    /// Saves the caller's settings for this instance (requires an instance id). False when the
    /// caller has no settings owner (e.g. anonymous). Limits: 50 values of 4000 characters.
    /// </summary>
    public async ValueTask<bool> SaveSettingsAsync(IReadOnlyDictionary<string, string> values, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (Widget.InstanceId is null)
            throw new InvalidOperationException("This widget has no instance id, so it has no settings.");
        if (values.Count > MaxSettings || values.Any(v => v.Key.Length > 100 || v.Value.Length > MaxSettingLength))
            throw new ArgumentException("Too many or too large widget settings.", nameof(values));
        if (await Widget.GetSettingsOwnerAsync(cancellationToken).ConfigureAwait(false) is not { } owner)
            return false;
        await Widget.SettingsStore.SaveAsync(new WebWidgetSettings(owner, Widget.InstanceId, Widget.Name, values, DateTimeOffset.UtcNow), cancellationToken).ConfigureAwait(false);
        Widget.Forget();
        return true;
    }

    /// <summary>Re-renders the widget as a fragment (the client swaps it in place).</summary>
    public async ValueTask<WebResult> RefreshAsync(CancellationToken cancellationToken) =>
        WebResult.Html(await _renderer.RenderInnerAsync(Widget, cancellationToken).ConfigureAwait(false)).WithHeader("X-WebLogic-Fragment", "1");
}

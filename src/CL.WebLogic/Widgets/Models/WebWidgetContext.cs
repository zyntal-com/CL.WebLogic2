using CL.WebLogic.Dashboard.Services;
using CL.WebLogic.Routing.Models;
using CL.WebLogic.Runtime.Models;
using CL.WebLogic.Security.Identity;
using CL.WebLogic.Security.Pipeline;
using CL.WebLogic.Widgets.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace CL.WebLogic.Widgets.Models;

/// <summary>Passed to a widget when it renders.</summary>
public sealed class WebWidgetContext
{
    private readonly IWebWidgetSettingsStore _settings;
    private WebWidgetSettings? _loaded;
    private bool _loadedOnce;
    private string? _owner;
    private bool _ownerResolved;

    internal WebWidgetContext(WebWidgetDefinition widget, string? instanceId, IReadOnlyDictionary<string, string> parameters, HttpContext httpContext, IWebWidgetSettingsStore settings)
    {
        Widget = widget;
        InstanceId = instanceId;
        Parameters = parameters;
        HttpContext = httpContext;
        _settings = settings;
    }

    /// <summary>The widget.</summary>
    public WebWidgetDefinition Widget { get; }

    /// <summary>Widget name.</summary>
    public string Name => Widget.Name;

    /// <summary>Who registered the widget.</summary>
    public WebContributorDescriptor Contributor => Widget.Contributor;

    /// <summary>Instance id (null for ad-hoc widgets).</summary>
    public string? InstanceId { get; }

    /// <summary>Parameters (template attributes, area parameters or query values — treat as untrusted).</summary>
    public IReadOnlyDictionary<string, string> Parameters { get; }

    /// <summary>The HTTP request.</summary>
    public HttpContext HttpContext { get; }

    /// <summary>The route context, when the widget renders inside a WebLogic route.</summary>
    public WebRequestContext? Request => HttpContext.Features.Get<WebRequestContext>();

    /// <summary>The caller.</summary>
    public WebIdentity Identity => WebSecurityContext.Get(HttpContext)?.Identity ?? WebIdentity.Anonymous;

    /// <summary>A parameter or <paramref name="defaultValue"/>.</summary>
    public string? GetParameter(string key, string? defaultValue = null) => Parameters.TryGetValue(key, out var value) ? value : defaultValue;

    /// <summary>A service.</summary>
    public T GetRequiredService<T>() where T : notnull => HttpContext.RequestServices.GetRequiredService<T>();

    /// <summary>
    /// The caller's saved settings for this instance (empty without an instance id or without a
    /// settings owner — anonymous callers under the default resolver).
    /// </summary>
    public async ValueTask<IReadOnlyDictionary<string, string>> GetSettingsAsync(CancellationToken cancellationToken)
    {
        if (InstanceId is null || await GetSettingsOwnerAsync(cancellationToken).ConfigureAwait(false) is not { } owner)
            return new Dictionary<string, string>();
        if (!_loadedOnce)
        {
            _loaded = await _settings.GetAsync(owner, InstanceId, cancellationToken).ConfigureAwait(false);
            _loadedOnce = true;
        }
        return _loaded is not null && string.Equals(_loaded.WidgetName, Name, StringComparison.OrdinalIgnoreCase) ? _loaded.Values : new Dictionary<string, string>();
    }

    /// <summary>
    /// Whose settings this request reads and writes: the dashboard owner
    /// (<see cref="IWebDashboardOwnerResolver"/>, key <c>widgets</c>), or null when the caller has none.
    /// </summary>
    public async ValueTask<string?> GetSettingsOwnerAsync(CancellationToken cancellationToken)
    {
        if (!_ownerResolved)
        {
            var resolver = HttpContext.RequestServices.GetService<IWebDashboardOwnerResolver>();
            _owner = resolver is null ? null : await resolver.ResolveAsync(HttpContext, SettingsDashboardKey, cancellationToken).ConfigureAwait(false);
            _ownerResolved = true;
        }
        return _owner;
    }

    /// <summary>The dashboard key passed to the owner resolver for widget settings.</summary>
    public const string SettingsDashboardKey = "widgets";

    internal IWebWidgetSettingsStore SettingsStore => _settings;

    internal void Forget()
    {
        _loaded = null;
        _loadedOnce = false;
    }
}

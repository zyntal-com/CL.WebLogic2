using System.Text;
using System.Text.Json;
using CL.WebLogic.Runtime.Services;
using CL.WebLogic.Dashboard.Models;
using CL.WebLogic.Security.Authorization;
using CL.WebLogic.Security.Pipeline;
using CL.WebLogic.Templates.Escaping;
using CL.WebLogic.Widgets.Services;
using CodeLogic.Core.Results;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Dashboard.Services;

/// <summary>
/// Reads, saves and renders dashboards through the app's store. Keys are validated, the owner
/// comes from <see cref="IWebDashboardOwnerResolver"/>, and saved items must name existing
/// dashboard widgets the caller may use.
/// </summary>
public sealed class WebDashboardService
{
    private const int MaxParameters = 20;
    private const int MaxParameterLength = 500;

    private readonly IWebDashboardLayoutStore _store;
    private readonly IWebDashboardOwnerResolver _owners;
    private readonly IWebDashboardDefaults _defaults;
    private readonly WebWidgetRegistry _widgets;
    private readonly WebWidgetRenderer _renderer;
    private readonly WebAccessEvaluator _access;

    /// <summary>Creates the service.</summary>
    public WebDashboardService(IWebDashboardLayoutStore store, IWebDashboardOwnerResolver owners, IWebDashboardDefaults defaults,
        WebWidgetRegistry widgets, WebWidgetRenderer renderer, WebAccessEvaluator access)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _owners = owners ?? throw new ArgumentNullException(nameof(owners));
        _defaults = defaults ?? throw new ArgumentNullException(nameof(defaults));
        _widgets = widgets ?? throw new ArgumentNullException(nameof(widgets));
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _access = access ?? throw new ArgumentNullException(nameof(access));
    }

    /// <summary>The caller's layout of <paramref name="dashboardKey"/> (stored, or the defaults with version 0).</summary>
    public async Task<Result<WebDashboardLayout>> GetAsync(HttpContext httpContext, string dashboardKey, CancellationToken cancellationToken)
    {
        var owner = await OwnerAsync(httpContext, dashboardKey, cancellationToken).ConfigureAwait(false);
        if (owner.IsFailure)
            return Result<WebDashboardLayout>.Failure(owner.Error!);
        var stored = await _store.GetAsync(owner.Value!, dashboardKey, cancellationToken).ConfigureAwait(false);
        return Result<WebDashboardLayout>.Success(stored ?? new WebDashboardLayout
        {
            OwnerKey = owner.Value!,
            DashboardKey = dashboardKey,
            // Defaults get stable ids, so widget settings saved before the first layout save still match.
            Items = WebDashboardLayouts.Normalize((await _defaults.GetAsync(dashboardKey, cancellationToken).ConfigureAwait(false))
                .Select((item, position) => string.IsNullOrEmpty(item.InstanceId)
                    ? item with { InstanceId = WebDashboardLayouts.DefaultInstanceId(owner.Value!, dashboardKey, position, item.WidgetName) }
                    : item))
        });
    }

    /// <summary>Validates and saves the caller's items.</summary>
    public async Task<Result<WebDashboardLayout>> SaveAsync(HttpContext httpContext, string dashboardKey, IReadOnlyList<WebDashboardItem> items, long expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(items);
        var owner = await OwnerAsync(httpContext, dashboardKey, cancellationToken).ConfigureAwait(false);
        if (owner.IsFailure)
            return Result<WebDashboardLayout>.Failure(owner.Error!);
        if (items.Count > WebDashboardLayouts.MaxItems)
            return Invalid($"A dashboard holds at most {WebDashboardLayouts.MaxItems} items.");

        var security = WebSecurityContext.Get(httpContext);
        foreach (var item in items)
        {
            var widget = _widgets.Find(item.WidgetName);
            if (widget is null || !widget.Dashboard)
                return Invalid($"'{item.WidgetName}' is not a dashboard widget.");
            if (!await _access.AllowsAsync(security, widget.Access, cancellationToken).ConfigureAwait(false))
                return Invalid($"'{item.WidgetName}' is not available to you.");
            if (!WebDashboardLayouts.IsValidZone(item.Zone))
                return Invalid($"Zone '{item.Zone}' is not valid.");
            if (item.InstanceId.Length > 0 && !WebWidgetRegistry.IsValidInstanceId(item.InstanceId))
                return Invalid($"Instance id '{item.InstanceId}' is not valid.");
            if (item.Parameters.Count > MaxParameters || item.Parameters.Any(p => p.Key.Length > 100 || p.Value.Length > MaxParameterLength))
                return Invalid("Too many or too long widget parameters.");
        }

        var layout = new WebDashboardLayout { OwnerKey = owner.Value!, DashboardKey = dashboardKey, Items = WebDashboardLayouts.Normalize(items) };
        return await _store.SaveAsync(layout, expectedVersion, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Renders the caller's dashboard: a <c>[data-wl-dashboard]</c> element with one
    /// <c>[data-wl-dashboard-zone]</c> per zone (<paramref name="zones"/> first, in order) and the
    /// widgets inside. Empty when the caller has no dashboard.
    /// </summary>
    public async Task<string> RenderAsync(HttpContext httpContext, string dashboardKey, IReadOnlyList<string>? zones, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        var result = await GetAsync(httpContext, dashboardKey, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
            return string.Empty;
        var layout = result.Value!;
        var zoneNames = (zones ?? []).Concat(layout.Items.Select(i => i.Zone)).Distinct(StringComparer.OrdinalIgnoreCase).DefaultIfEmpty("main").ToList();
        var basePath = httpContext.Request.PathBase.Value ?? "";

        var html = new StringBuilder("<div class=\"wl-dashboard\" data-wl-dashboard=\"").Append(A(dashboardKey))
            .Append("\" data-wl-dashboard-url=\"").Append(A(basePath + DashboardContributor.Prefix + Uri.EscapeDataString(dashboardKey)))
            .Append("\" data-wl-dashboard-version=\"").Append(layout.Version).Append("\">");
        foreach (var zone in zoneNames)
        {
            html.Append("<section class=\"wl-dashboard-zone\" data-wl-dashboard-zone=\"").Append(A(zone)).Append("\">");
            foreach (var item in layout.Items.Where(i => string.Equals(i.Zone, zone, StringComparison.OrdinalIgnoreCase)))
            {
                var attributes = "data-wl-dashboard-item=\"" + A(item.InstanceId) + "\" data-wl-dashboard-widget=\"" + A(item.WidgetName)
                    + "\" data-wl-dashboard-params=\"" + A(JsonSerializer.Serialize(item.Parameters, WebJson.Default)) + "\"";
                html.Append(await _renderer.RenderAsync(httpContext, item.WidgetName, item.InstanceId, item.Parameters, attributes, cancellationToken).ConfigureAwait(false));
            }
            html.Append("</section>");
        }
        return html.Append("</div>").ToString();
    }

    /// <summary>Widgets the caller may add to dashboards.</summary>
    public async Task<IReadOnlyList<Widgets.Models.WebWidgetDefinition>> AvailableWidgetsAsync(HttpContext httpContext, CancellationToken cancellationToken)
    {
        var security = WebSecurityContext.Get(httpContext);
        var available = new List<Widgets.Models.WebWidgetDefinition>();
        foreach (var widget in _widgets.Widgets.Where(w => w.Dashboard).OrderBy(w => w.Name, StringComparer.Ordinal))
        {
            if (await _access.AllowsAsync(security, widget.Access, cancellationToken).ConfigureAwait(false))
                available.Add(widget);
        }
        return available;
    }

    private async Task<Result<string>> OwnerAsync(HttpContext httpContext, string dashboardKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        if (!WebDashboardLayouts.IsValidKey(dashboardKey))
            return Result<string>.Failure(Error.Validation(WebDashboardErrors.InvalidKeyCode, "The dashboard key is not valid."));
        var owner = await _owners.ResolveAsync(httpContext, dashboardKey, cancellationToken).ConfigureAwait(false);
        if (owner is null)
            return Result<string>.Failure(Error.Unauthorized(WebDashboardErrors.NoOwnerCode, "Sign in to use this dashboard."));
        if (!WebDashboardLayouts.IsValidKey(owner))
            throw new InvalidOperationException("IWebDashboardOwnerResolver returned an invalid owner key.");
        return Result<string>.Success(owner);
    }

    private static Result<WebDashboardLayout> Invalid(string message) =>
        Result<WebDashboardLayout>.Failure(Error.Validation(WebDashboardErrors.InvalidCode, message));

    private static string A(string value) => TemplateEncoder.Html.Encode(value);
}

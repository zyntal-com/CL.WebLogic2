using System.Text.Json;
using CL.WebLogic.Results;
using CL.WebLogic.Routing;
using CL.WebLogic.Routing.Models;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Runtime.Models;
using CL.WebLogic.Security.Authorization;
using CL.WebLogic.Widgets.Models;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Widgets.Services;

/// <summary>
/// Widget endpoints, always registered: <c>GET /_weblogic/widgets/{name}</c> (re-render as a
/// fragment) and <c>POST /_weblogic/widgets/{name}/actions/{action}</c> (CSRF-protected). Access is
/// decided per widget.
/// </summary>
public sealed class WidgetsContributor : IWebContributor
{
    /// <summary>Contributor id.</summary>
    public const string ContributorId = "weblogic.widgets";

    private const int MaxParameters = 20;
    private const int MaxParameterLength = 500;
    private const long MaxActionBytes = 64 * 1024;

    private readonly WebWidgetRegistry _registry;
    private readonly WebWidgetRenderer _renderer;
    private readonly WebAccessEvaluator _access;

    /// <summary>Creates the contributor.</summary>
    public WidgetsContributor(WebWidgetRegistry registry, WebWidgetRenderer renderer, WebAccessEvaluator access)
    {
        _access = access ?? throw new ArgumentNullException(nameof(access));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
    }

    /// <summary>Descriptor.</summary>
    public static WebContributorDescriptor Descriptor { get; } = new()
    {
        Id = ContributorId,
        Name = "WebLogic widgets",
        Kind = "System",
        Description = "Widget refresh and action endpoints."
    };

    /// <inheritdoc/>
    public Task ContributeAsync(WebContributionBuilder builder, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.MapFragment("/_weblogic/widgets/{name}", RenderAsync)
            .AllowAnonymous()
            .WithName("weblogic.widgets.render")
            .WithDescription("Re-renders a widget (access per widget)");
        builder.MapApi("/_weblogic/widgets/{name}/actions/{action}", ActionAsync, "POST")
            .AllowAnonymous()
            .WithBodyLimit(MaxActionBytes)
            .WithName("weblogic.widgets.action")
            .WithDescription("Runs a widget action (access per widget)");
        return Task.CompletedTask;
    }

    private async ValueTask<WebResult> RenderAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        var (context, denied) = await ResolveAsync(request, cancellationToken).ConfigureAwait(false);
        if (context is null)
            return denied!;
        return WebResult.Html(await _renderer.RenderInnerAsync(context, cancellationToken).ConfigureAwait(false)).WithHeader("X-WebLogic-Fragment", "1");
    }

    private async ValueTask<WebResult> ActionAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        var (context, denied) = await ResolveAsync(request, cancellationToken).ConfigureAwait(false);
        if (context is null)
            return denied!;
        var actionName = request.GetRouteValue("action");
        if (actionName is null || !context.Widget.Actions.TryGetValue(actionName, out var handler))
            return WebResult.Problem(StatusCodes.Status404NotFound, "Unknown widget action");

        JsonElement payload;
        var body = await request.ReadBodyAsStringAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body, new JsonDocumentOptions { MaxDepth = 16 });
            payload = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return WebResult.Problem(StatusCodes.Status400BadRequest, "The action payload is not valid JSON");
        }
        return await handler(new WebWidgetActionContext(actionName, context, payload, _renderer), cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<(WebWidgetContext? Context, WebResult? Denied)> ResolveAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        var widget = _registry.Find(request.GetRouteValue("name"));
        if (widget is null)
            return (null, WebResult.Problem(StatusCodes.Status404NotFound, "Unknown widget"));

        var query = request.HttpContext.Request.Query;
        var instance = query.TryGetValue("instance", out var instanceValues) ? instanceValues.ToString() : null;
        if (instance is not null && !WebWidgetRegistry.IsValidInstanceId(instance))
            return (null, WebResult.Problem(StatusCodes.Status400BadRequest, "Invalid widget instance id"));
        if (query.Count > MaxParameters + 1)
            return (null, WebResult.Problem(StatusCodes.Status400BadRequest, "Too many widget parameters"));
        var parameters = query.Where(q => q.Key != "instance")
            .ToDictionary(q => q.Key, q => q.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        if (parameters.Any(p => p.Key.Length > 100 || p.Value.Length > MaxParameterLength))
            return (null, WebResult.Problem(StatusCodes.Status400BadRequest, "Widget parameter too long"));

        var context = await _renderer.CreateContextAsync(request.HttpContext, widget.Name, instance, parameters, cancellationToken).ConfigureAwait(false);
        if (context is not null && await PlacementAllowsAsync(request, widget, instance, cancellationToken).ConfigureAwait(false))
            return (context, null);
        return (null, request.Identity.IsAuthenticated
            ? WebResult.Problem(StatusCodes.Status403Forbidden)
            : WebResult.Problem(StatusCodes.Status401Unauthorized, "Sign in to use this widget"));
    }

    /// <summary>
    /// Placement rules also hold at the endpoint: a placed instance needs its placement's access,
    /// and any other call needs at least one placement the caller may see when every placement has
    /// a rule (dashboard widgets too). Query parameters stay untrusted input for the widget.
    /// </summary>
    private async ValueTask<bool> PlacementAllowsAsync(WebRequestContext request, WebWidgetDefinition widget, string? instance, CancellationToken cancellationToken)
    {
        if (instance is not null && _registry.Placements.FirstOrDefault(p => p.InstanceId == instance
                && string.Equals(p.WidgetName, widget.Name, StringComparison.OrdinalIgnoreCase)) is { } placed)
            return placed.Access is null || await _access.AllowsAsync(request.Security, placed.Access, cancellationToken).ConfigureAwait(false);
        return await WebWidgetPlacementAccess.AllowsAsync(_registry, _access, request.Security, widget, cancellationToken).ConfigureAwait(false);
    }
}

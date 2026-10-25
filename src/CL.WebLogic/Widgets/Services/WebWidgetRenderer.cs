using System.Text;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Security.Authorization;
using CL.WebLogic.Security.Pipeline;
using CL.WebLogic.Templates.Escaping;
using CL.WebLogic.Templates.Rendering;
using CL.WebLogic.Widgets.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace CL.WebLogic.Widgets.Services;

/// <summary>
/// Renders widgets (<c>{widget:name attr="…"}</c>) and widget areas (<c>{widgetarea:name}</c>).
/// Widgets the caller may not see render nothing; a failing widget is logged and replaced by an
/// HTML comment so it never breaks the page.
/// </summary>
public sealed class WebWidgetRenderer : ITemplateWidgetRenderer
{
    private const int MinRefreshSeconds = 5;
    private readonly WebWidgetRegistry _registry;
    private readonly WebAccessEvaluator _access;
    private readonly IWebWidgetSettingsStore _settings;
    private readonly IWebLogicContext _context;
    private readonly IServiceProvider _services;

    /// <summary>Creates the renderer.</summary>
    public WebWidgetRenderer(WebWidgetRegistry registry, WebAccessEvaluator access, IWebWidgetSettingsStore settings, IWebLogicContext context, IServiceProvider services)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _access = access ?? throw new ArgumentNullException(nameof(access));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <inheritdoc/>
    public async Task RenderWidgetAsync(TemplateRenderContext context, string name, IReadOnlyDictionary<string, string> attributes)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(attributes);
        if (context.HttpContext is null)
            return;
        var parameters = attributes.Where(a => !string.Equals(a.Key, "instance", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(a => a.Key, a => a.Value, StringComparer.OrdinalIgnoreCase);
        var instance = attributes.GetValueOrDefault("instance");
        context.Output.Append(await RenderAsync(context.HttpContext, name, instance, parameters, null, context.CancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc/>
    public async Task RenderAreaAsync(TemplateRenderContext context, string area)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.HttpContext is null)
            return;
        var path = context.HttpContext.Request.Path.Value ?? "/";
        var security = WebSecurityContext.Get(context.HttpContext);
        var html = new StringBuilder();
        foreach (var placement in _registry.PlacementsFor(area))
        {
            if (!WebPathPattern.Passes(path, placement.IncludePaths, placement.ExcludePaths))
                continue;
            if (placement.Access is not null && !await _access.AllowsAsync(security, placement.Access, context.CancellationToken).ConfigureAwait(false))
                continue;
            html.Append(await RenderAsync(context.HttpContext, placement.WidgetName, placement.InstanceId, placement.Parameters, null, context.CancellationToken).ConfigureAwait(false));
        }
        if (html.Length > 0)
            context.Output.Append("<div class=\"wl-widget-area\" data-wl-widget-area=\"").Append(TemplateEncoder.Html.Encode(area)).Append("\">").Append(html).Append("</div>");
    }

    /// <summary>
    /// Renders a widget with its wrapper element; empty when it is unknown or the caller may not
    /// see it. <paramref name="extraAttributes"/> is trusted markup added to the wrapper.
    /// </summary>
    public async Task<string> RenderAsync(HttpContext httpContext, string name, string? instanceId, IReadOnlyDictionary<string, string> parameters, string? extraAttributes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        var context = await CreateContextAsync(httpContext, name, instanceId, parameters, cancellationToken).ConfigureAwait(false);
        if (context is null)
            return string.Empty;

        var inner = await RenderInnerAsync(context, cancellationToken).ConfigureAwait(false);
        var widget = context.Widget;
        var html = new StringBuilder("<div class=\"wl-widget\" data-wl-widget=\"").Append(A(widget.Name)).Append('"');
        if (instanceId is not null)
            html.Append(" data-wl-widget-instance=\"").Append(A(instanceId)).Append('"');
        if (widget.RefreshSeconds > 0)
        {
            html.Append(" data-wl-refresh=\"").Append(A(WebWidgetUrls.Refresh(httpContext.Request.PathBase.Value ?? "", widget.Name, instanceId, parameters)))
                .Append("\" data-wl-refresh-interval=\"").Append(Math.Max(MinRefreshSeconds, widget.RefreshSeconds)).Append('"');
        }
        if (extraAttributes is not null)
            html.Append(' ').Append(extraAttributes);
        return html.Append('>').Append(inner).Append("</div>").ToString();
    }

    /// <summary>A widget context when the widget exists, the ids are valid and the caller may see it; otherwise null.</summary>
    public async Task<WebWidgetContext?> CreateContextAsync(HttpContext httpContext, string name, string? instanceId, IReadOnlyDictionary<string, string> parameters, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        var widget = _registry.Find(name);
        if (widget is null || (instanceId is not null && !WebWidgetRegistry.IsValidInstanceId(instanceId)))
            return null;
        if (!await _access.AllowsAsync(WebSecurityContext.Get(httpContext), widget.Access, cancellationToken).ConfigureAwait(false))
            return null;
        return new WebWidgetContext(widget, instanceId, parameters, httpContext, _settings);
    }

    /// <summary>The widget's own markup (no wrapper).</summary>
    public async Task<string> RenderInnerAsync(WebWidgetContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            var result = await context.Widget.Handler(context, cancellationToken).ConfigureAwait(false);
            if (result.TemplatePath is null)
                return result.Html ?? string.Empty;
            return await _services.GetRequiredService<IWebTemplateRenderer>().RenderAsync(new TemplateRenderRequest
            {
                TemplatePath = result.TemplatePath,
                Model = result.Model,
                HttpContext = context.HttpContext,
                IgnoreLayout = true
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _context.Logger.Error($"Widget '{context.Name}' failed: {ex.Message}", ex);
            return "<!-- widget failed: " + A(context.Name) + " -->";
        }
    }

    private static string A(string value) => TemplateEncoder.Html.Encode(value);
}

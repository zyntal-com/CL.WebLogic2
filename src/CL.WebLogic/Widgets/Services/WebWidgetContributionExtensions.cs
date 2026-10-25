using CL.WebLogic.Routing.Services;
using CL.WebLogic.Widgets.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CL.WebLogic.Widgets.Services;

/// <summary>Widget registration from contributors and plugins (removed with the contribution).</summary>
public static class WebWidgetContributionExtensions
{
    /// <summary>Registers a widget.</summary>
    public static WebWidgetDefinition MapWidget(this WebContributionBuilder builder, string name, WebWidgetHandler handler, Action<WebWidgetOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(handler);
        if (!WebWidgetRegistry.IsValidName(name))
            throw new ArgumentException($"Widget name '{name}' must match ^[a-z0-9][a-z0-9._-]{{0,63}}$.", nameof(name));
        var options = new WebWidgetOptions();
        configure?.Invoke(options);

        var widget = new WebWidgetDefinition
        {
            Name = name,
            Contributor = builder.Contributor,
            Handler = handler,
            Description = options.Description,
            Tags = [.. options.Tags],
            Access = options.Access,
            SampleParameters = new Dictionary<string, string>(options.SampleParameters, StringComparer.OrdinalIgnoreCase),
            RefreshSeconds = options.RefreshSeconds,
            Dashboard = options.Dashboard,
            Actions = new Dictionary<string, WebWidgetActionHandler>(options.Actions, StringComparer.OrdinalIgnoreCase)
        };
        var registry = builder.Services.GetRequiredService<WebWidgetRegistry>();
        builder.OnCommit(() => registry.Add(widget));
        builder.OnRemove(() =>
        {
            registry.Remove(widget);
            return ValueTask.CompletedTask;
        });
        return widget;
    }

    /// <summary>Places a widget in a widget area.</summary>
    public static WebWidgetPlacement PlaceWidget(this WebContributionBuilder builder, string area, string widgetName, Action<WebWidgetPlacementOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(area);
        ArgumentException.ThrowIfNullOrEmpty(widgetName);
        var options = new WebWidgetPlacementOptions();
        configure?.Invoke(options);
        if (options.InstanceId is not null && !WebWidgetRegistry.IsValidInstanceId(options.InstanceId))
            throw new ArgumentException($"Instance id '{options.InstanceId}' is not valid.", nameof(configure));

        var placement = new WebWidgetPlacement
        {
            Area = area,
            WidgetName = widgetName,
            InstanceId = options.InstanceId,
            Contributor = builder.Contributor,
            Order = options.Order,
            Parameters = new Dictionary<string, string>(options.Parameters, StringComparer.OrdinalIgnoreCase),
            IncludePaths = [.. options.IncludePaths],
            ExcludePaths = [.. options.ExcludePaths],
            Access = options.Access
        };
        var registry = builder.Services.GetRequiredService<WebWidgetRegistry>();
        builder.OnCommit(() => registry.AddPlacement(placement));
        builder.OnRemove(() =>
        {
            registry.RemovePlacement(placement);
            return ValueTask.CompletedTask;
        });
        return placement;
    }
}

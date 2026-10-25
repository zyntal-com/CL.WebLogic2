namespace CL.WebLogic.Templates.Rendering;

/// <summary>Renders <c>{widget:…}</c> and <c>{widgetarea:…}</c> directives.</summary>
public interface ITemplateWidgetRenderer
{
    /// <summary>Renders one widget with its (already resolved) attributes into the output.</summary>
    Task RenderWidgetAsync(TemplateRenderContext context, string name, IReadOnlyDictionary<string, string> attributes);

    /// <summary>Renders every widget registered for <paramref name="area"/>.</summary>
    Task RenderAreaAsync(TemplateRenderContext context, string area);
}

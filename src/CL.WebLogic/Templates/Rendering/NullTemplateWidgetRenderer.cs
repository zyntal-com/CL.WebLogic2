using CL.WebLogic.Templates.Escaping;
using System.Text.Encodings.Web;

namespace CL.WebLogic.Templates.Rendering;

/// <summary>Placeholder until widgets are registered: writes an HTML comment.</summary>
public sealed class NullTemplateWidgetRenderer : ITemplateWidgetRenderer
{
    /// <inheritdoc/>
    public Task RenderWidgetAsync(TemplateRenderContext context, string name, IReadOnlyDictionary<string, string> attributes)
    {
        context.Output.Append("<!-- widget not available: ").Append(TemplateEncoder.Html.Encode(name)).Append(" -->");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task RenderAreaAsync(TemplateRenderContext context, string area) => Task.CompletedTask;
}

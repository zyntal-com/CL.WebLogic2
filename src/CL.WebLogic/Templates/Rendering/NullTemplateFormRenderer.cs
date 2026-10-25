using CL.WebLogic.Templates.Escaping;

namespace CL.WebLogic.Templates.Rendering;

/// <summary>Used when no form renderer is registered: writes an HTML comment.</summary>
public sealed class NullTemplateFormRenderer : ITemplateFormRenderer
{
    /// <summary>Shared instance.</summary>
    public static NullTemplateFormRenderer Instance { get; } = new();

    /// <inheritdoc/>
    public Task RenderFormAsync(TemplateRenderContext context, string name, IReadOnlyDictionary<string, string> attributes)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Output.Append("<!-- form not available: ").Append(TemplateEncoder.Html.Encode(name)).Append(" -->");
        return Task.CompletedTask;
    }
}

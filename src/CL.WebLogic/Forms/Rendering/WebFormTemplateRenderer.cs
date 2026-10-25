using CL.WebLogic.Forms.Services;
using CL.WebLogic.Runtime.Services;
using CL.WebLogic.Templates.Escaping;
using CL.WebLogic.Templates.Rendering;

namespace CL.WebLogic.Forms.Rendering;

/// <summary><c>{form:id}</c>: renders a registered form with the request's remembered state.</summary>
public sealed class WebFormTemplateRenderer : ITemplateFormRenderer
{
    private readonly WebFormRegistry _registry;
    private readonly WebFormService _forms;

    /// <summary>Creates the renderer.</summary>
    public WebFormTemplateRenderer(WebFormRegistry registry, WebFormService forms)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _forms = forms ?? throw new ArgumentNullException(nameof(forms));
    }

    /// <inheritdoc/>
    public async Task RenderFormAsync(TemplateRenderContext context, string name, IReadOnlyDictionary<string, string> attributes)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(attributes);
        var type = _registry.Find(name);
        if (type is null || context.HttpContext is null)
        {
            context.Output.Append("<!-- form not registered: ").Append(TemplateEncoder.Html.Encode(name)).Append(" -->");
            return;
        }

        var action = attributes.GetValueOrDefault("action");
        if (action is not null && !WebUrl.IsLocal(action))
            action = null;
        var options = new WebFormRenderOptions
        {
            Action = action,
            CssClass = attributes.GetValueOrDefault("class"),
            SubmitLabel = attributes.GetValueOrDefault("submit"),
            Renderer = attributes.GetValueOrDefault("renderer")
        };
        var html = await _forms.RenderAsync(context.HttpContext, _forms.Definitions.Get(type), null, options, context.CancellationToken).ConfigureAwait(false);
        context.Output.Append(html);
    }
}

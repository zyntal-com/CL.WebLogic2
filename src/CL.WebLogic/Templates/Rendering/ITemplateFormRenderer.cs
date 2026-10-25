namespace CL.WebLogic.Templates.Rendering;

/// <summary>Renders <c>{form:id}</c>. The forms feature supplies the implementation.</summary>
public interface ITemplateFormRenderer
{
    /// <summary>Writes the form to <see cref="TemplateRenderContext.Output"/>.</summary>
    Task RenderFormAsync(TemplateRenderContext context, string name, IReadOnlyDictionary<string, string> attributes);
}

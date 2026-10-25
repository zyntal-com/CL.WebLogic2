namespace CL.WebLogic.Templates.Rendering;

/// <summary>Renders theme templates (with layouts, sections and partials) to HTML.</summary>
public interface IWebTemplateRenderer
{
    /// <summary>Renders <paramref name="request"/>.</summary>
    Task<string> RenderAsync(TemplateRenderRequest request, CancellationToken cancellationToken);

    /// <summary>True when the template exists in the active theme.</summary>
    bool Exists(string templatePath);
}

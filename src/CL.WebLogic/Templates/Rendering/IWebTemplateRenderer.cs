namespace CL.WebLogic.Templates.Rendering;

/// <summary>Renders theme templates (with layouts, sections and partials) to HTML.</summary>
public interface IWebTemplateRenderer
{
    /// <summary>Renders <paramref name="request"/>.</summary>
    Task<string> RenderAsync(TemplateRenderRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Renders <paramref name="request"/> and keeps the result in its buffer, so a page can be written to the response without
    /// one more copy into a string. The caller disposes the result. The default renders to a string.
    /// </summary>
    async Task<RenderedTemplate> RenderBufferedAsync(TemplateRenderRequest request, CancellationToken cancellationToken) =>
        RenderedTemplate.FromText(await RenderAsync(request, cancellationToken).ConfigureAwait(false));

    /// <summary>True when the template exists in the active theme.</summary>
    bool Exists(string templatePath);
}

namespace CL.WebLogic.Templates.Rendering;

/// <summary>Loads and caches compiled templates by normalized path.</summary>
public interface ITemplateLoader
{
    /// <summary>Loads <paramref name="normalizedPath"/>; throws <see cref="TemplateRenderException"/> when it does not exist.</summary>
    ValueTask<CompiledTemplate> LoadAsync(string normalizedPath, CancellationToken cancellationToken);

    /// <summary>True when the template exists.</summary>
    bool Exists(string normalizedPath);
}

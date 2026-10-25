using System.Collections.Concurrent;
using CL.WebLogic.Templates.Parsing;
using CL.WebLogic.Templates.Rendering;

namespace CL.WebLogic.Testing.Services;

/// <summary>An <see cref="ITemplateLoader"/> over in-memory sources, for unit-testing templates.</summary>
public sealed class InMemoryTemplateLoader : ITemplateLoader
{
    private readonly ConcurrentDictionary<string, string> _sources = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, CompiledTemplate> _compiled = new(StringComparer.OrdinalIgnoreCase);
    private readonly ITemplateCompiler _compiler;

    /// <summary>Creates the loader; templates are compiled with <paramref name="compiler"/> (closure compiler by default).</summary>
    public InMemoryTemplateLoader(ITemplateCompiler? compiler = null) => _compiler = compiler ?? new ClosureTemplateCompiler();

    /// <summary>Adds or replaces a template (<c>.html</c> is optional in <paramref name="path"/>).</summary>
    public InMemoryTemplateLoader Add(string path, string source)
    {
        var normalized = TemplatePath.Normalize(path);
        _sources[normalized] = source ?? string.Empty;
        _compiled.TryRemove(normalized, out _);
        return this;
    }

    /// <inheritdoc/>
    public bool Exists(string normalizedPath) => _sources.ContainsKey(normalizedPath);

    /// <inheritdoc/>
    public ValueTask<CompiledTemplate> LoadAsync(string normalizedPath, CancellationToken cancellationToken)
    {
        if (!_sources.TryGetValue(normalizedPath, out var source))
            throw new TemplateRenderException("Template not found.", normalizedPath);

        var compiled = _compiled.GetOrAdd(normalizedPath, path =>
            _compiler.Compile(path, TemplateSourceHash.Compute(source), TemplateParser.Parse(source, path)));
        return ValueTask.FromResult(compiled);
    }
}

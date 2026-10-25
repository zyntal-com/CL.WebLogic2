namespace CL.WebLogic.Templates.Rendering;

/// <summary>A template ready to render: its body, its named sections and an optional layout.</summary>
public sealed class CompiledTemplate
{
    /// <summary>Creates the template.</summary>
    public CompiledTemplate(string path, string sourceHash, string? layoutPath, TemplateBlock body, IReadOnlyDictionary<string, TemplateBlock> sections, string engine)
    {
        Path = path;
        SourceHash = sourceHash;
        LayoutPath = layoutPath;
        Body = body;
        Sections = sections;
        Engine = engine;
    }

    /// <summary>Normalized theme-relative path.</summary>
    public string Path { get; }

    /// <summary><see cref="Parsing.TemplateSourceHash"/> of the source.</summary>
    public string SourceHash { get; }

    /// <summary>Layout path as written in <c>{layout:…}</c>, or null.</summary>
    public string? LayoutPath { get; }

    /// <summary>The body.</summary>
    public TemplateBlock Body { get; }

    /// <summary>Sections for the layout.</summary>
    public IReadOnlyDictionary<string, TemplateBlock> Sections { get; }

    /// <summary>Engine that produced it (<c>interpreter</c>, <c>closure</c>, <c>generated</c>).</summary>
    public string Engine { get; }
}

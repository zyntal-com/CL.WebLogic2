using CL.WebLogic.ClientBundler.Lexing;

namespace CL.WebLogic.ClientBundler.Linking;

/// <summary>A parsed module: its body (imports and export syntax removed), imports and exports.</summary>
internal sealed class JsModule
{
    /// <summary>Path relative to the source root, with forward slashes (the module key).</summary>
    public required string Path { get; init; }

    /// <summary>Body tokens with <c>import</c> statements replaced and <c>export</c> keywords removed.</summary>
    public required List<JsToken> Body { get; init; }

    /// <summary>Imports, in source order.</summary>
    public required IReadOnlyList<ModuleImport> Imports { get; init; }

    /// <summary>Exported name → local binding.</summary>
    public required IReadOnlyDictionary<string, string> Exports { get; init; }
}

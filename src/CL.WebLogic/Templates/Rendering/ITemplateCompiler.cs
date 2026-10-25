using CL.WebLogic.Templates.Ast;

namespace CL.WebLogic.Templates.Rendering;

/// <summary>Turns a parsed template into a <see cref="CompiledTemplate"/>.</summary>
public interface ITemplateCompiler
{
    /// <summary>Engine name.</summary>
    string Name { get; }

    /// <summary>Compiles <paramref name="document"/>.</summary>
    CompiledTemplate Compile(string path, string sourceHash, TemplateDocument document);
}

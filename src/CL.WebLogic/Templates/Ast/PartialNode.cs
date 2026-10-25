namespace CL.WebLogic.Templates.Ast;

/// <summary><c>{partial:path key="v" rows=model:x}</c> — includes another template with optional parameters.</summary>
public sealed class PartialNode : TemplateNode
{
    /// <summary>Creates the node.</summary>
    public PartialNode(string path, IReadOnlyList<PartialArgument> arguments)
    {
        Path = path;
        Arguments = arguments;
    }

    /// <summary>Partial path as written.</summary>
    public string Path { get; }

    /// <summary>Parameters.</summary>
    public IReadOnlyList<PartialArgument> Arguments { get; }
}

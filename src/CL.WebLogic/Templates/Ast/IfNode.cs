namespace CL.WebLogic.Templates.Ast;

/// <summary><c>{if:…}…{elseif:…}…{else}…{/if}</c> (and <c>{ifnot:…}</c>).</summary>
public sealed class IfNode : TemplateNode
{
    /// <summary>Creates the node.</summary>
    public IfNode(IReadOnlyList<ConditionalBranch> branches, IReadOnlyList<TemplateNode>? @else)
    {
        Branches = branches;
        Else = @else;
    }

    /// <summary>Branches in order; the first that holds renders.</summary>
    public IReadOnlyList<ConditionalBranch> Branches { get; }

    /// <summary>Rendered when no branch holds, or null.</summary>
    public IReadOnlyList<TemplateNode>? Else { get; }
}

namespace CL.WebLogic.Templates.Ast;

/// <summary><c>{csrf}</c>, <c>{csrf_token}</c> or <c>{csrf_meta}</c>.</summary>
public sealed class CsrfNode : TemplateNode
{
    /// <summary>Creates the node.</summary>
    public CsrfNode(CsrfKind kind) => Kind = kind;

    /// <summary>Which markup to emit.</summary>
    public CsrfKind Kind { get; }
}

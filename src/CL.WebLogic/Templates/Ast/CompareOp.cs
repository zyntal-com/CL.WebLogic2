namespace CL.WebLogic.Templates.Ast;

/// <summary>Comparison operator for <c>{if:a gt b}</c>.</summary>
public enum CompareOp
{
    /// <summary>Equal.</summary>
    Eq,

    /// <summary>Not equal.</summary>
    Ne,

    /// <summary>Greater than.</summary>
    Gt,

    /// <summary>Greater than or equal.</summary>
    Gte,

    /// <summary>Less than.</summary>
    Lt,

    /// <summary>Less than or equal.</summary>
    Lte
}

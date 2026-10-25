namespace CL.WebLogic.Templates.Ast;

/// <summary>One <c>{case:value}…{/case}</c>.</summary>
public sealed class SwitchCase
{
    /// <summary>Creates the case.</summary>
    public SwitchCase(string value, IReadOnlyList<TemplateNode> body)
    {
        Value = value;
        Body = body;
    }

    /// <summary>Value compared case-insensitively with the subject.</summary>
    public string Value { get; }

    /// <summary>Rendered when matched.</summary>
    public IReadOnlyList<TemplateNode> Body { get; }
}

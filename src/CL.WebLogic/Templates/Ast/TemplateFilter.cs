namespace CL.WebLogic.Templates.Ast;

/// <summary>One filter in a token's pipe chain, e.g. <c>truncate:100</c>.</summary>
public sealed class TemplateFilter
{
    /// <summary>Creates the filter.</summary>
    public TemplateFilter(string name, string? arg)
    {
        Name = name;
        Arg = arg;
    }

    /// <summary>Filter name (case-insensitive).</summary>
    public string Name { get; }

    /// <summary>Optional argument.</summary>
    public string? Arg { get; }
}

namespace CL.WebLogic.Templates.Ast;

/// <summary>A named <c>{section:name}…{/section}</c> block captured for the layout.</summary>
public sealed class SectionDefinition
{
    /// <summary>Creates the section.</summary>
    public SectionDefinition(string name, IReadOnlyList<TemplateNode> body)
    {
        Name = name;
        Body = body;
    }

    /// <summary>Section name.</summary>
    public string Name { get; }

    /// <summary>Section content.</summary>
    public IReadOnlyList<TemplateNode> Body { get; }
}

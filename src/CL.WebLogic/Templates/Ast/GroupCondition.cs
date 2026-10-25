namespace CL.WebLogic.Templates.Ast;

/// <summary><c>{if:group:admin,editor}</c> — the user is in any listed group.</summary>
public sealed class GroupCondition : TemplateCondition
{
    /// <summary>Creates the condition.</summary>
    public GroupCondition(IReadOnlyList<string> groups) => Groups = groups;

    /// <summary>Accepted groups.</summary>
    public IReadOnlyList<string> Groups { get; }
}

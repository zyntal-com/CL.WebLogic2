namespace CL.WebLogic.Templates.Ast;

/// <summary>A parsed selector such as <c>model:user.name</c>, <c>item:score</c> or a bare <c>rows</c>.</summary>
public sealed class TemplateSelector
{
    /// <summary>Creates the selector.</summary>
    public TemplateSelector(TemplateScope scope, string key, string? alias = null)
    {
        Scope = scope;
        Key = key ?? string.Empty;
        Alias = alias;
    }

    /// <summary>The scope.</summary>
    public TemplateScope Scope { get; }

    /// <summary>The (dotted) path after the scope prefix.</summary>
    public string Key { get; }

    /// <summary>The loop alias name when <see cref="Scope"/> is <see cref="TemplateScope.Alias"/>.</summary>
    public string? Alias { get; }

    /// <inheritdoc/>
    public override string ToString() => Scope switch
    {
        TemplateScope.None => Key,
        TemplateScope.Alias => Alias + ":" + Key,
        _ => Scope.ToString().ToLowerInvariant() + ":" + Key
    };
}

namespace CL.WebLogic.Templates.Ast;

/// <summary><c>{if:permission:posts.edit}</c> — the user holds the permission.</summary>
public sealed class PermissionCondition : TemplateCondition
{
    /// <summary>Creates the condition.</summary>
    public PermissionCondition(string permission) => Permission = permission;

    /// <summary>Required permission.</summary>
    public string Permission { get; }
}

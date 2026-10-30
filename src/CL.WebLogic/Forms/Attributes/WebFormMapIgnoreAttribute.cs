namespace CL.WebLogic.Forms.Attributes;

/// <summary>
/// Excludes a property from <see cref="Services.WebFormMapper"/> in both directions. Put it on
/// sensitive entity properties (password hash, security stamp, owner id, roles) so no form can ever
/// write them, even one that happens to have a field with the same name; or on a form property that
/// should not be copied.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class WebFormMapIgnoreAttribute : Attribute
{
}

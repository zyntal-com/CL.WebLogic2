namespace CL.WebLogic.Forms.Attributes;

/// <summary>Excludes a property from the form (never bound — protects against over-posting).</summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class WebIgnoreAttribute : Attribute
{
}

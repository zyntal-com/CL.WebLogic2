namespace CL.WebLogic.Forms.Attributes;

/// <summary>Shows (and validates) the field only while <paramref name="field"/> has one of <paramref name="values"/>.</summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class WebShowWhenAttribute(string field, params string[] values) : Attribute
{
    /// <summary>The controlling field.</summary>
    public string Field { get; } = field;

    /// <summary>Values that show this field ("true" for a checked checkbox).</summary>
    public IReadOnlyList<string> Values { get; } = values;
}

namespace CL.WebLogic.Forms.Attributes;

/// <summary>A list property of a class type, rendered as a repeated group of its fields.</summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class WebRepeaterAttribute : Attribute
{
    /// <summary>Minimum items.</summary>
    public int Min { get; set; }

    /// <summary>Maximum items.</summary>
    public int Max { get; set; } = 50;
}

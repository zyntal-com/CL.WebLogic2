namespace CL.WebLogic.Forms.Attributes;

/// <summary>Puts the field on a step of a multi-step form. Steps are ordered by <see cref="Order"/>, then first use.</summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class WebStepAttribute(string name) : Attribute
{
    /// <summary>Step name.</summary>
    public string Name { get; } = name;

    /// <summary>Step title (default: the name).</summary>
    public string? Title { get; set; }

    /// <summary>Step order.</summary>
    public int Order { get; set; } = int.MaxValue;
}

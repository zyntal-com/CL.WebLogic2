namespace CL.WebLogic.Forms.Attributes;

/// <summary>Marks a class as a form model.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class WebFormAttribute : Attribute
{
    /// <summary>Form id (default: the type name in kebab-case, without a "Form" suffix).</summary>
    public string? Id { get; set; }

    /// <summary>Title.</summary>
    public string? Title { get; set; }

    /// <summary>Submit button text.</summary>
    public string? SubmitLabel { get; set; }

    /// <summary>Apply the honeypot and timing checks (default true).</summary>
    public bool AntiBot { get; set; } = true;
}

using CL.WebLogic.Forms.Models;

namespace CL.WebLogic.Forms.Attributes;

/// <summary>Rendering details of a field (validation comes from DataAnnotations).</summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class WebFieldAttribute : Attribute
{
    /// <summary>Creates the attribute.</summary>
    public WebFieldAttribute()
    {
    }

    /// <summary>Creates the attribute with a type.</summary>
    public WebFieldAttribute(WebFieldType type)
    {
        Type = type;
        HasType = true;
    }

    /// <summary>Rendering type (inferred from the property type when not set).</summary>
    public WebFieldType Type { get; }

    internal bool HasType { get; }

    /// <summary>Label.</summary>
    public string? Label { get; set; }

    /// <summary>Placeholder.</summary>
    public string? Placeholder { get; set; }

    /// <summary>Help text.</summary>
    public string? Help { get; set; }

    /// <summary>HTML autocomplete hint (e.g. <c>email</c>, <c>new-password</c>).</summary>
    public string? Autocomplete { get; set; }

    /// <summary>Sort order (default: declaration order).</summary>
    public int Order { get; set; } = int.MinValue;

    /// <summary>Step for number inputs.</summary>
    public string? Step { get; set; }

    /// <summary>Read-only.</summary>
    public bool ReadOnly { get; set; }

    /// <summary>Validate on the server when the field loses focus (runs the form's <c>IWebFormValidator</c>s for this field).</summary>
    public bool Remote { get; set; }
}

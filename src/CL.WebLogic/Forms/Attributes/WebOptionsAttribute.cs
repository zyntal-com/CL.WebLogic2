namespace CL.WebLogic.Forms.Attributes;

/// <summary>
/// Choices of a field: fixed (<c>"value"</c> or <c>"value|Label"</c>) or from a named options
/// provider (<see cref="Provider"/>). Text properties become selects; use
/// <c>[WebField(WebFieldType.Autocomplete)]</c> for type-ahead search instead. Enum properties
/// get their options automatically.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class WebOptionsAttribute : Attribute
{
    /// <summary>Fixed options.</summary>
    public WebOptionsAttribute(params string[] options) => Options = options;

    /// <summary>Fixed options.</summary>
    public IReadOnlyList<string> Options { get; }

    /// <summary>Name of an <c>IWebFormOptionsProvider</c>.</summary>
    public string? Provider { get; set; }
}

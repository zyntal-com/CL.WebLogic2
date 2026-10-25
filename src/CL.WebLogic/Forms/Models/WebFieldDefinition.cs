using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace CL.WebLogic.Forms.Models;

/// <summary>
/// One field of a form: how it binds (property, CLR type), renders (type, label, options) and
/// validates (rules from DataAnnotations and WebLogic attributes). Built once per model type.
/// </summary>
public sealed class WebFieldDefinition
{
    /// <summary>Form field name (the property name).</summary>
    public required string Name { get; init; }

    /// <summary>The bound property.</summary>
    public required PropertyInfo Property { get; init; }

    /// <summary>The property type without <see cref="Nullable{T}"/>.</summary>
    public required Type ValueType { get; init; }

    /// <summary>Element type for multi-value fields (lists, arrays) and repeaters.</summary>
    public Type? ElementType { get; init; }

    /// <summary>Rendering type.</summary>
    public WebFieldType Type { get; set; }

    /// <summary>Label.</summary>
    public string Label { get; set; } = "";

    /// <summary>Placeholder.</summary>
    public string? Placeholder { get; set; }

    /// <summary>Help text under the field.</summary>
    public string? Help { get; set; }

    /// <summary>HTML autocomplete hint.</summary>
    public string? Autocomplete { get; set; }

    /// <summary>Sort order.</summary>
    public int Order { get; set; }

    /// <summary>Required.</summary>
    public bool Required { get; set; }

    /// <summary>Minimum length.</summary>
    public int? MinLength { get; set; }

    /// <summary>Maximum length.</summary>
    public int? MaxLength { get; set; }

    /// <summary>Minimum (numbers, dates — invariant text).</summary>
    public string? Min { get; set; }

    /// <summary>Maximum.</summary>
    public string? Max { get; set; }

    /// <summary>Step for number/range inputs.</summary>
    public string? StepValue { get; set; }

    /// <summary>Regular expression the whole value must match (1 s timeout).</summary>
    public string? Pattern { get; set; }

    /// <summary>Custom message for <see cref="Pattern"/>.</summary>
    public string? PatternMessage { get; set; }

    /// <summary>Name of a field this one must equal.</summary>
    public string? EqualTo { get; set; }

    /// <summary>Read-only.</summary>
    public bool ReadOnly { get; set; }

    /// <summary>Validated on the server when it loses focus.</summary>
    public bool Remote { get; set; }

    /// <summary>Fixed options.</summary>
    public IReadOnlyList<WebFieldOption> Options { get; set; } = [];

    /// <summary>Options provider name (dynamic options / autocomplete search).</summary>
    public string? OptionsProvider { get; set; }

    /// <summary>Visibility condition.</summary>
    public WebShowWhen? ShowWhen { get; set; }

    /// <summary>Upload rules (file fields).</summary>
    public WebFileRules? File { get; set; }

    /// <summary>Sub-form of a repeater.</summary>
    public WebFormDefinition? Repeater { get; set; }

    /// <summary>Minimum repeater items.</summary>
    public int RepeaterMin { get; set; }

    /// <summary>Maximum repeater items.</summary>
    public int RepeaterMax { get; set; } = 50;

    /// <summary>Wizard step this field belongs to.</summary>
    public string? StepName { get; set; }

    /// <summary>Other DataAnnotations validators (run on the bound value).</summary>
    public IReadOnlyList<ValidationAttribute> Validators { get; set; } = [];

    /// <summary>Extra HTML attributes for the input.</summary>
    public Dictionary<string, string> InputAttributes { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>True for fields that post several values.</summary>
    public bool IsMultiple => Type is WebFieldType.CheckboxList or WebFieldType.MultiSelect || (Type == WebFieldType.File && (File?.MaxFiles ?? 1) > 1);
}

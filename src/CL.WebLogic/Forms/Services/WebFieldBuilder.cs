using CL.WebLogic.Forms.Models;

namespace CL.WebLogic.Forms.Services;

/// <summary>Fluent configuration of one field.</summary>
public sealed class WebFieldBuilder
{
    internal WebFieldBuilder(WebFieldDefinition field) => Field = field;

    /// <summary>The field being configured.</summary>
    public WebFieldDefinition Field { get; }

    /// <summary>Sets the label.</summary>
    public WebFieldBuilder Label(string label) => Do(() => Field.Label = label);

    /// <summary>Sets the rendering type.</summary>
    public WebFieldBuilder Type(WebFieldType type) => Do(() => Field.Type = type);

    /// <summary>Sets the placeholder.</summary>
    public WebFieldBuilder Placeholder(string placeholder) => Do(() => Field.Placeholder = placeholder);

    /// <summary>Sets the help text.</summary>
    public WebFieldBuilder Help(string help) => Do(() => Field.Help = help);

    /// <summary>Makes the field required (or not).</summary>
    public WebFieldBuilder Required(bool required = true) => Do(() => Field.Required = required);

    /// <summary>Sets length limits.</summary>
    public WebFieldBuilder Length(int? min, int? max) => Do(() =>
    {
        Field.MinLength = min;
        Field.MaxLength = max;
    });

    /// <summary>Sets a range (invariant text, e.g. "1" or "2026-01-01").</summary>
    public WebFieldBuilder Range(string? min, string? max) => Do(() =>
    {
        Field.Min = min;
        Field.Max = max;
    });

    /// <summary>Requires the whole value to match <paramref name="pattern"/>.</summary>
    public WebFieldBuilder Pattern(string pattern, string? message = null) => Do(() =>
    {
        Field.Pattern = pattern;
        Field.PatternMessage = message;
    });

    /// <summary>Sets fixed options.</summary>
    public WebFieldBuilder Options(params WebFieldOption[] options) => Do(() => Field.Options = options);

    /// <summary>Uses a named options provider.</summary>
    public WebFieldBuilder OptionsFrom(string provider) => Do(() => Field.OptionsProvider = provider);

    /// <summary>Shows the field only while <paramref name="field"/> has one of <paramref name="values"/>.</summary>
    public WebFieldBuilder ShowWhen(string field, params string[] values) => Do(() => Field.ShowWhen = new WebShowWhen(field, values));

    /// <summary>Puts the field on a wizard step (the step must exist).</summary>
    public WebFieldBuilder Step(string step) => Do(() => Field.StepName = step);

    /// <summary>Validates the field on the server when it loses focus.</summary>
    public WebFieldBuilder Remote(bool remote = true) => Do(() => Field.Remote = remote);

    /// <summary>Sets the sort order.</summary>
    public WebFieldBuilder Order(int order) => Do(() => Field.Order = order);

    /// <summary>Adds an HTML attribute to the input (event handler attributes are refused).</summary>
    public WebFieldBuilder Attribute(string name, string value)
    {
        if (name.StartsWith("on", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Event handler attributes are not allowed.", nameof(name));
        return Do(() => Field.InputAttributes[name] = value);
    }

    /// <summary>Sets upload rules.</summary>
    public WebFieldBuilder Files(WebFileRules rules) => Do(() => Field.File = rules);

    private WebFieldBuilder Do(Action change)
    {
        change();
        return this;
    }
}

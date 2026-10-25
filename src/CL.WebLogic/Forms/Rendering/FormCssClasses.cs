namespace CL.WebLogic.Forms.Rendering;

/// <summary>CSS classes a renderer uses.</summary>
public sealed record FormCssClasses
{
    /// <summary>Form element.</summary>
    public string Form { get; init; } = "";
    /// <summary>Field wrapper.</summary>
    public string Field { get; init; } = "";
    /// <summary>Label.</summary>
    public string Label { get; init; } = "";
    /// <summary>Text-like inputs and textareas.</summary>
    public string Input { get; init; } = "";
    /// <summary>Selects.</summary>
    public string Select { get; init; } = "";
    /// <summary>Range inputs.</summary>
    public string Range { get; init; } = "";
    /// <summary>Colour inputs.</summary>
    public string Color { get; init; } = "";
    /// <summary>Checkbox/radio wrapper.</summary>
    public string Check { get; init; } = "";
    /// <summary>Checkbox/radio input.</summary>
    public string CheckInput { get; init; } = "";
    /// <summary>Checkbox/radio label.</summary>
    public string CheckLabel { get; init; } = "";
    /// <summary>Help text.</summary>
    public string Help { get; init; } = "";
    /// <summary>Field error.</summary>
    public string Error { get; init; } = "";
    /// <summary>Invalid input marker.</summary>
    public string Invalid { get; init; } = "";
    /// <summary>Error summary.</summary>
    public string Summary { get; init; } = "";
    /// <summary>Fieldset (choice lists, repeaters).</summary>
    public string Fieldset { get; init; } = "";
    /// <summary>Legend.</summary>
    public string Legend { get; init; } = "";
    /// <summary>Repeater item.</summary>
    public string RepeaterItem { get; init; } = "";
    /// <summary>Wizard step list.</summary>
    public string Steps { get; init; } = "";
    /// <summary>Wizard step.</summary>
    public string Step { get; init; } = "";
    /// <summary>Current wizard step.</summary>
    public string StepCurrent { get; init; } = "";
    /// <summary>Button row.</summary>
    public string Actions { get; init; } = "";
    /// <summary>Primary button.</summary>
    public string Submit { get; init; } = "";
    /// <summary>Secondary button.</summary>
    public string Secondary { get; init; } = "";
}

namespace CL.WebLogic.Forms.Rendering;

/// <summary>Framework-free markup (styled by weblogic.css or the app).</summary>
public sealed class PlainFormRenderer(WebFormRendererServices services) : WebFormRenderer(services, Css)
{
    private static readonly FormCssClasses Css = new()
    {
        Form = "wl-form",
        Field = "wl-field",
        Label = "wl-label",
        Input = "wl-input",
        Select = "wl-input",
        Range = "wl-range",
        Color = "wl-color",
        Check = "wl-check",
        CheckInput = "wl-check__input",
        CheckLabel = "wl-check__label",
        Help = "wl-help",
        Error = "wl-field-error",
        Invalid = "wl-invalid",
        Summary = "wl-form-summary",
        Fieldset = "wl-fieldset",
        Legend = "wl-legend",
        RepeaterItem = "wl-repeater__item",
        Steps = "wl-steps",
        Step = "wl-step",
        StepCurrent = "wl-step wl-step--current",
        Actions = "wl-actions",
        Submit = "wl-button wl-button--primary",
        Secondary = "wl-button"
    };

    /// <inheritdoc/>
    public override string Name => "plain";
}

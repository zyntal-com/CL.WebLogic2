
namespace CL.WebLogic.Forms.Rendering;

/// <summary>Bootstrap 5 markup.</summary>
public sealed class Bootstrap5FormRenderer(WebFormRendererServices services) : WebFormRenderer(services, Css)
{
    private static readonly FormCssClasses Css = new()
    {
        Form = "wl-form",
        Field = "mb-3",
        Label = "form-label",
        Input = "form-control",
        Select = "form-select",
        Range = "form-range",
        Color = "form-control form-control-color",
        Check = "form-check",
        CheckInput = "form-check-input",
        CheckLabel = "form-check-label",
        Help = "form-text",
        Error = "invalid-feedback d-block",
        Invalid = "is-invalid",
        Summary = "alert alert-danger",
        Fieldset = "mb-3",
        Legend = "form-label",
        RepeaterItem = "border rounded p-3 mb-2",
        Steps = "nav nav-pills mb-3",
        Step = "nav-link disabled",
        StepCurrent = "nav-link active",
        Actions = "d-flex gap-2",
        Submit = "btn btn-primary",
        Secondary = "btn btn-outline-secondary"
    };

    /// <inheritdoc/>
    public override string Name => "bootstrap5";
}

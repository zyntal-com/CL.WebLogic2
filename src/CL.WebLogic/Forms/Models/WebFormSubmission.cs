using CL.WebLogic.Results;
using Microsoft.Extensions.Primitives;

namespace CL.WebLogic.Forms.Models;

/// <summary>
/// A bound and validated submission. For multi-step forms <see cref="IsComplete"/> is false until
/// the last step passed; return <see cref="Continue"/> to show the next (or previous) step.
/// </summary>
public sealed class WebFormSubmission<TModel>
{
    internal WebFormSubmission(WebFormDefinition definition, TModel model, WebFormErrors errors, IReadOnlyDictionary<string, StringValues> values)
    {
        Definition = definition;
        Model = model;
        Errors = errors;
        Values = values;
    }

    /// <summary>The form.</summary>
    public WebFormDefinition Definition { get; }

    /// <summary>The bound model (fields of later wizard steps are still empty while not complete).</summary>
    public TModel Model { get; }

    /// <summary>Errors.</summary>
    public WebFormErrors Errors { get; }

    /// <summary>The posted (and carried-over) raw values, for re-rendering.</summary>
    public IReadOnlyDictionary<string, StringValues> Values { get; }

    /// <summary>True when there are no errors.</summary>
    public bool IsValid => !Errors.Any;

    /// <summary>True when the anti-bot checks rejected the submission.</summary>
    public bool Rejected { get; internal set; }

    /// <summary>False while a multi-step form has more steps to go.</summary>
    public bool IsComplete { get; internal set; } = true;

    /// <summary>The step to render next (multi-step forms).</summary>
    public int StepIndex { get; internal set; }

    /// <summary>The state token for <see cref="StepIndex"/>.</summary>
    public string? WizardToken { get; internal set; }

    /// <summary>The rendered form at <see cref="StepIndex"/> (multi-step forms).</summary>
    public string? StepHtml { get; internal set; }

    /// <summary>Adds an error (field "" = form-level), e.g. after a failed save.</summary>
    public WebFormSubmission<TModel> AddError(string field, string message)
    {
        Errors.Add(field, message);
        return this;
    }

    /// <summary>The "fix the errors" result: JSON for the client, or <paramref name="fallbackTemplate"/> re-rendered (status 422) for plain posts.</summary>
    public FormResult Invalid(string? summary = null, string? fallbackTemplate = null, object? fallbackModel = null) =>
        FormResult.Failure(Definition.Id, Errors, summary, fallbackTemplate, fallbackModel);

    /// <summary>
    /// For an incomplete multi-step form: shows the next (or previous) step, or the errors.
    /// Plain (non-script) posts get <paramref name="fallbackTemplate"/> re-rendered.
    /// </summary>
    public FormResult Continue(string? fallbackTemplate = null, object? fallbackModel = null) => IsValid
        ? FormResult.Step(Definition.Id, Definition.Steps[StepIndex].Name, StepHtml ?? "", fallbackTemplate, fallbackModel)
        : Invalid(fallbackTemplate: fallbackTemplate, fallbackModel: fallbackModel);
}

using Microsoft.Extensions.Primitives;

namespace CL.WebLogic.Forms.Models;

/// <summary>What a form shows when it is rendered in this request: values, errors and wizard position.</summary>
public sealed class WebFormRenderState
{
    /// <summary>Raw values.</summary>
    public IReadOnlyDictionary<string, StringValues> Values { get; init; } = new Dictionary<string, StringValues>();

    /// <summary>Errors.</summary>
    public WebFormErrors? Errors { get; init; }

    /// <summary>Wizard step.</summary>
    public int StepIndex { get; init; }

    /// <summary>Wizard state token.</summary>
    public string? WizardToken { get; init; }

    /// <summary>The <c>HttpContext.Items</c> key of a form's state.</summary>
    public static string ItemKey(string formId) => "wl.forms.state:" + formId;
}

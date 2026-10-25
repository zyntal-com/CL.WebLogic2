using CL.WebLogic.Forms.Models;
using CL.WebLogic.Runtime.Models;

namespace CL.WebLogic.Forms.Validation;

/// <summary>Passed to <see cref="IWebFormValidator{TModel}"/>.</summary>
public sealed class WebFormValidationContext
{
    internal WebFormValidationContext(WebFormDefinition definition, WebFormErrors errors, WebRequestContext? request, string? onlyField)
    {
        Definition = definition;
        Errors = errors;
        Request = request;
        OnlyField = onlyField;
    }

    /// <summary>The form.</summary>
    public WebFormDefinition Definition { get; }

    /// <summary>The errors so far.</summary>
    public WebFormErrors Errors { get; }

    /// <summary>The request (null outside HTTP).</summary>
    public WebRequestContext? Request { get; }

    /// <summary>Set for remote validation of a single field: only report errors for it.</summary>
    public string? OnlyField { get; }

    /// <summary>Adds an error to a field ("" for the whole form).</summary>
    public void AddError(string field, string message)
    {
        if (OnlyField is null || string.Equals(OnlyField, field, StringComparison.OrdinalIgnoreCase))
            Errors.Add(field, message);
    }
}

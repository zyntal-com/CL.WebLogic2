using CL.WebLogic.Forms.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace CL.WebLogic.Forms.Rendering;

/// <summary>What to render.</summary>
public sealed class WebFormRenderRequest
{
    /// <summary>The form.</summary>
    public required WebFormDefinition Definition { get; init; }

    /// <summary>The request (CSRF token, anti-bot timestamp).</summary>
    public HttpContext? HttpContext { get; init; }

    /// <summary>Form action (default: the current path).</summary>
    public string? Action { get; init; }

    /// <summary>Values to show.</summary>
    public IReadOnlyDictionary<string, StringValues> Values { get; init; } = new Dictionary<string, StringValues>();

    /// <summary>Errors to show.</summary>
    public WebFormErrors? Errors { get; init; }

    /// <summary>Wizard step to render.</summary>
    public int StepIndex { get; init; }

    /// <summary>Wizard state token.</summary>
    public string? WizardToken { get; init; }

    /// <summary>Submit button text override.</summary>
    public string? SubmitLabel { get; init; }

    /// <summary>Extra CSS class on the form element.</summary>
    public string? CssClass { get; init; }

    /// <summary>Options loaded from providers, by field name.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<WebFieldOption>> ProviderOptions { get; init; } = new Dictionary<string, IReadOnlyList<WebFieldOption>>();
}

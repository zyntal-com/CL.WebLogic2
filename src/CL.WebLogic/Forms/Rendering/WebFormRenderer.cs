using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CL.WebLogic.Forms.AntiBot;
using CL.WebLogic.Forms.Models;
using CL.WebLogic.Forms.Services;
using CL.WebLogic.Forms.Wizards;
using CL.WebLogic.Runtime.Services;
using CL.WebLogic.Templates.Escaping;

namespace CL.WebLogic.Forms.Rendering;

/// <summary>
/// Shared form markup: schema block, CSRF and anti-bot fields, wizard steps, error summary,
/// fields with native validation attributes and ARIA, and the buttons. Subclasses supply the
/// CSS classes. Every value is HTML-encoded; field ids and error ids match the client's scheme.
/// </summary>
public abstract partial class WebFormRenderer : IWebFormRenderer
{
    private readonly WebFormRendererServices _services;

    /// <summary>Creates the renderer.</summary>
    protected WebFormRenderer(WebFormRendererServices services, FormCssClasses classes)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        Classes = classes ?? throw new ArgumentNullException(nameof(classes));
    }

    /// <inheritdoc/>
    public abstract string Name { get; }

    /// <summary>CSS classes.</summary>
    protected FormCssClasses Classes { get; }

    /// <inheritdoc/>
    public async ValueTask<string> RenderAsync(WebFormRenderRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var definition = request.Definition;
        var formId = FormElementId(definition);
        var steps = definition.Steps;
        var stepIndex = steps.Count == 0 ? 0 : Math.Clamp(request.StepIndex, 0, steps.Count - 1);
        var fields = definition.FieldsOf(steps.Count == 0 ? null : steps[stepIndex].Name).ToList();
        var errors = request.Errors ?? new WebFormErrors();
        var visible = WebFormVisibility.For(definition, request.Values);
        var action = request.Action ?? ((request.HttpContext?.Request.PathBase.Value ?? "") + (request.HttpContext?.Request.Path.Value ?? ""));
        var hasFiles = fields.Any(f => f.Type == WebFieldType.File || (f.Repeater?.Fields.Any(c => c.Type == WebFieldType.File) ?? false));

        var html = new StringBuilder();
        html.Append("<form id=\"").Append(A(formId)).Append("\" class=\"").Append(A(Join(Classes.Form, request.CssClass)))
            .Append("\" method=\"post\" action=\"").Append(A(action)).Append("\" data-wl-form=\"").Append(A(definition.Id)).Append('"');
        if (hasFiles)
            html.Append(" enctype=\"multipart/form-data\"");
        html.Append(">\n");

        // The default encoder escapes <, > and &, so the JSON cannot close the script element.
        html.Append("<script type=\"application/json\" data-wl-form-schema>")
            .Append(JsonSerializer.Serialize(WebFormSchema.Build(definition, fields, _services.Messages), WebJson.Default))
            .Append("</script>\n");
        AppendHiddenFields(html, request, definition, steps.Count > 0);
        if (steps.Count > 0)
            AppendSteps(html, steps, stepIndex);

        html.Append("<div data-wl-form-summary>");
        if (errors.Any)
        {
            var summary = errors.Form.Count > 0 ? string.Join(' ', errors.Form) : _services.Messages.Template("summary");
            html.Append("<div class=\"").Append(A(Classes.Summary)).Append("\" role=\"alert\" data-wl-error=\"\">").Append(A(summary)).Append("</div>");
        }
        html.Append("</div>\n");

        foreach (var field in fields)
            await AppendFieldAsync(html, request, field, "", formId, errors, visible, cancellationToken).ConfigureAwait(false);

        var last = steps.Count == 0 || stepIndex == steps.Count - 1;
        html.Append("<div class=\"").Append(A(Classes.Actions)).Append("\">");
        if (stepIndex > 0)
            html.Append("<button type=\"submit\" class=\"").Append(A(Classes.Secondary)).Append("\" name=\"").Append(WebWizardState.BackField).Append("\" value=\"1\" formnovalidate>Back</button>");
        html.Append("<button type=\"submit\" class=\"").Append(A(Classes.Submit)).Append("\">")
            .Append(A(request.SubmitLabel ?? (last ? definition.SubmitLabel : "Next"))).Append("</button></div>\n");
        html.Append("</form>");
        return html.ToString();
    }

    /// <summary>The id of the form element.</summary>
    public static string FormElementId(WebFormDefinition definition) => "wl-form-" + (definition ?? throw new ArgumentNullException(nameof(definition))).Id;

    /// <summary>The HTML input type of a field type.</summary>
    public static string InputType(WebFieldType type) => type switch
    {
        WebFieldType.Email => "email",
        WebFieldType.Password => "password",
        WebFieldType.Number => "number",
        WebFieldType.Range => "range",
        WebFieldType.Date => "date",
        WebFieldType.Time => "time",
        WebFieldType.DateTimeLocal => "datetime-local",
        WebFieldType.Color => "color",
        WebFieldType.Tel => "tel",
        WebFieldType.Url => "url",
        WebFieldType.Hidden => "hidden",
        WebFieldType.Checkbox => "checkbox",
        WebFieldType.Radio => "radio",
        WebFieldType.File => "file",
        WebFieldType.Select => "select",
        WebFieldType.MultiSelect => "select",
        WebFieldType.CheckboxList => "checkbox",
        WebFieldType.TextArea or WebFieldType.RichText => "textarea",
        _ => "text"
    };

    private void AppendHiddenFields(StringBuilder html, WebFormRenderRequest request, WebFormDefinition definition, bool wizard)
    {
        var config = _services.Config.Current;
        if (request.HttpContext is { } http)
            Hidden(html, config.Security.Csrf.FieldName, _services.Csrf.GetToken(http));
        if (definition.AntiBot)
        {
            if (config.Forms.Honeypot)
            {
                html.Append("<div hidden aria-hidden=\"true\"><label>Leave this field empty <input type=\"text\" name=\"")
                    .Append(WebFormAntiBot.HoneypotField).Append("\" value=\"\" tabindex=\"-1\" autocomplete=\"off\"></label></div>\n");
            }
            Hidden(html, WebFormAntiBot.TimestampField, _services.AntiBot.IssueTimestamp(definition.Id));
        }
        if (wizard && request.WizardToken is not null)
            Hidden(html, WebWizardState.TokenField, request.WizardToken);
    }

    private void AppendSteps(StringBuilder html, IReadOnlyList<WebFormStep> steps, int current)
    {
        html.Append("<ol class=\"").Append(A(Classes.Steps)).Append("\" data-wl-form-steps>");
        for (var i = 0; i < steps.Count; i++)
        {
            html.Append("<li class=\"").Append(A(i == current ? Classes.StepCurrent : Classes.Step)).Append('"');
            if (i == current)
                html.Append(" aria-current=\"step\"");
            html.Append('>').Append(A(steps[i].Title)).Append("</li>");
        }
        html.Append("</ol>\n");
    }

    private static void Hidden(StringBuilder html, string name, string value) =>
        html.Append("<input type=\"hidden\" name=\"").Append(A(name)).Append("\" value=\"").Append(A(value)).Append("\">\n");

    /// <summary>HTML-encodes text for content or attributes.</summary>
    protected static string A(string? value) => TemplateEncoder.Html.Encode(value ?? string.Empty);

    /// <summary>A field key as an id fragment (same rule as the client).</summary>
    protected static string IdPart(string key) => NonIdChars().Replace(key, "_");

    private static string Join(params string?[] classes) => string.Join(' ', classes.Where(c => !string.IsNullOrWhiteSpace(c)));

    [GeneratedRegex(@"[^\w-]", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex NonIdChars();
}

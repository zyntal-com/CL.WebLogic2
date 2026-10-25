using System.Globalization;
using System.Text;
using System.Text.Json;
using CL.WebLogic.Forms.Models;
using CL.WebLogic.Runtime.Services;
using CL.WebLogic.Templates.Escaping;
using CL.WebLogic.Templates.Rendering;
using Microsoft.Extensions.Primitives;

namespace CL.WebLogic.Forms.Rendering;

public abstract partial class WebFormRenderer
{
    private async ValueTask AppendFieldAsync(StringBuilder html, WebFormRenderRequest request, WebFieldDefinition field, string prefix, string formId,
        WebFormErrors errors, Func<string, bool> visible, CancellationToken cancellationToken)
    {
        var key = prefix + field.Name;
        request.Values.TryGetValue(key, out var values);
        if (field.Type == WebFieldType.Hidden)
        {
            Hidden(html, key, values.Count > 0 ? values[0] ?? "" : "");
            return;
        }
        if (field.Repeater is not null)
        {
            await AppendRepeaterAsync(html, request, field, key, formId, errors, visible, cancellationToken).ConfigureAwait(false);
            return;
        }

        var id = formId + "-" + IdPart(key);
        var errorId = "wl-err-" + formId + "-" + IdPart(key);
        var fieldErrors = errors.For(key);
        var control = Control(request, field, key, id, values, fieldErrors.Count > 0, errorId);

        html.Append("<div class=\"").Append(A(Classes.Field)).Append("\" data-wl-field=\"").Append(A(key)).Append('"');
        AppendShowWhen(html, field, key, visible);
        html.Append('>');

        var themed = await ThemedAsync(request, field, key, id, values, fieldErrors, control, errorId, cancellationToken).ConfigureAwait(false);
        if (themed is not null)
        {
            html.Append(themed);
        }
        else
        {
            if (field.Type is not (WebFieldType.Checkbox or WebFieldType.Radio or WebFieldType.CheckboxList))
                AppendLabel(html, field, id);
            html.Append(control);
            if (!string.IsNullOrEmpty(field.Help))
                html.Append("<div class=\"").Append(A(Classes.Help)).Append("\" id=\"").Append(A(id)).Append("-help\">").Append(A(field.Help)).Append("</div>");
            if (fieldErrors.Count > 0)
                AppendError(html, key, errorId, fieldErrors);
        }
        html.Append("</div>\n");
    }

    private string Control(WebFormRenderRequest request, WebFieldDefinition field, string key, string id, StringValues values, bool invalid, string errorId)
    {
        var value = values.Count > 0 ? values[0] ?? "" : "";
        var describedBy = string.Join(' ', new[] { string.IsNullOrEmpty(field.Help) ? null : id + "-help", invalid ? errorId : null }.Where(s => s is not null));
        var options = request.ProviderOptions.TryGetValue(field.Name, out var loaded) ? loaded : field.Options;
        var html = new StringBuilder();

        switch (field.Type)
        {
            case WebFieldType.TextArea:
            case WebFieldType.RichText:
                html.Append("<textarea class=\"").Append(A(Join(Classes.Input, invalid ? Classes.Invalid : null))).Append('"');
                Common(html, field, key, id, invalid, describedBy);
                Length(html, field);
                if (field.Type == WebFieldType.RichText) html.Append(" data-wl-richtext");
                html.Append(" rows=\"4\">").Append(A(value)).Append("</textarea>");
                break;

            case WebFieldType.Select:
            case WebFieldType.MultiSelect:
                html.Append("<select class=\"").Append(A(Join(Classes.Select, invalid ? Classes.Invalid : null))).Append('"');
                Common(html, field, key, id, invalid, describedBy);
                if (field.Type == WebFieldType.MultiSelect) html.Append(" multiple");
                html.Append('>');
                if (field.Type == WebFieldType.Select)
                    html.Append("<option value=\"\">").Append(A(field.Placeholder ?? "Choose…")).Append("</option>");
                foreach (var group in options.GroupBy(o => o.Group))
                {
                    if (group.Key is not null) html.Append("<optgroup label=\"").Append(A(group.Key)).Append("\">");
                    foreach (var option in group)
                    {
                        html.Append("<option value=\"").Append(A(option.Value)).Append('"');
                        if (values.Contains(option.Value, StringComparer.OrdinalIgnoreCase)) html.Append(" selected");
                        if (option.Disabled) html.Append(" disabled");
                        html.Append('>').Append(A(option.Label)).Append("</option>");
                    }
                    if (group.Key is not null) html.Append("</optgroup>");
                }
                html.Append("</select>");
                break;

            case WebFieldType.Checkbox:
                html.Append("<div class=\"").Append(A(Classes.Check)).Append("\"><input type=\"checkbox\" class=\"").Append(A(Join(Classes.CheckInput, invalid ? Classes.Invalid : null))).Append("\" value=\"true\"");
                Common(html, field, key, id, invalid, describedBy);
                if (value is "true" or "on" or "1") html.Append(" checked");
                html.Append("><label class=\"").Append(A(Classes.CheckLabel)).Append("\" for=\"").Append(A(id)).Append("\">").Append(A(field.Label));
                if (field.Required) html.Append(" <span aria-hidden=\"true\">*</span>");
                html.Append("</label></div>");
                break;

            case WebFieldType.Radio:
            case WebFieldType.CheckboxList:
                var type = field.Type == WebFieldType.Radio ? "radio" : "checkbox";
                html.Append("<fieldset class=\"").Append(A(Classes.Fieldset)).Append('"');
                if (describedBy.Length > 0) html.Append(" aria-describedby=\"").Append(A(describedBy)).Append('"');
                html.Append("><legend class=\"").Append(A(Classes.Legend)).Append("\">").Append(A(field.Label));
                if (field.Required) html.Append(" <span aria-hidden=\"true\">*</span>");
                html.Append("</legend>");
                var index = 0;
                foreach (var option in options)
                {
                    var optionId = id + "-" + index++;
                    html.Append("<div class=\"").Append(A(Classes.Check)).Append("\"><input type=\"").Append(type).Append("\" class=\"")
                        .Append(A(Join(Classes.CheckInput, invalid ? Classes.Invalid : null))).Append("\" id=\"").Append(A(optionId))
                        .Append("\" name=\"").Append(A(key)).Append("\" value=\"").Append(A(option.Value)).Append('"');
                    if (values.Contains(option.Value, StringComparer.OrdinalIgnoreCase)) html.Append(" checked");
                    if (option.Disabled || field.ReadOnly) html.Append(" disabled");
                    if (invalid) html.Append(" aria-invalid=\"true\"");
                    if (field.Required && field.Type == WebFieldType.Radio) html.Append(" required");
                    html.Append("><label class=\"").Append(A(Classes.CheckLabel)).Append("\" for=\"").Append(A(optionId)).Append("\">").Append(A(option.Label)).Append("</label></div>");
                }
                html.Append("</fieldset>");
                break;

            case WebFieldType.File:
                html.Append("<input type=\"file\" class=\"").Append(A(Join(Classes.Input, invalid ? Classes.Invalid : null))).Append('"');
                Common(html, field, key, id, invalid, describedBy);
                if ((field.File?.MaxFiles ?? 1) > 1) html.Append(" multiple");
                var accept = field.File?.Extensions.Count > 0 ? field.File.Extensions : (field.File?.ImagesOnly == true ? ["image/*"] : []);
                if (accept.Count > 0) html.Append(" accept=\"").Append(A(string.Join(',', accept))).Append('"');
                html.Append('>');
                break;

            default:
                var inputType = InputType(field.Type);
                var css = field.Type switch { WebFieldType.Range => Classes.Range, WebFieldType.Color => Classes.Color, _ => Classes.Input };
                html.Append("<input type=\"").Append(inputType).Append("\" class=\"").Append(A(Join(css, invalid ? Classes.Invalid : null))).Append('"');
                Common(html, field, key, id, invalid, describedBy);
                if (field.Type != WebFieldType.Password && value.Length > 0) html.Append(" value=\"").Append(A(value)).Append('"');
                Length(html, field);
                if (field.Min is not null) html.Append(" min=\"").Append(A(field.Min)).Append('"');
                if (field.Max is not null) html.Append(" max=\"").Append(A(field.Max)).Append('"');
                if (field.StepValue is not null) html.Append(" step=\"").Append(A(field.StepValue)).Append('"');
                if (field.Pattern is not null) html.Append(" pattern=\"").Append(A(field.Pattern)).Append('"');
                if (field.Type == WebFieldType.Autocomplete && field.OptionsProvider is not null)
                    html.Append(" data-wl-autocomplete=\"").Append(A(field.OptionsProvider)).Append("\" role=\"combobox\" aria-autocomplete=\"list\" aria-expanded=\"false\"");
                html.Append('>');
                break;
        }
        return html.ToString();
    }

    private static void Common(StringBuilder html, WebFieldDefinition field, string key, string id, bool invalid, string describedBy)
    {
        html.Append(" id=\"").Append(A(id)).Append("\" name=\"").Append(A(key)).Append('"');
        if (field.Required) html.Append(" required");
        if (invalid) html.Append(" aria-invalid=\"true\"");
        if (describedBy.Length > 0) html.Append(" aria-describedby=\"").Append(A(describedBy)).Append('"');
        if (field.ReadOnly) html.Append(field.Type is WebFieldType.Select or WebFieldType.MultiSelect or WebFieldType.Checkbox or WebFieldType.File ? " disabled" : " readonly");
        if (field.Placeholder is not null && field.Type is not (WebFieldType.Select or WebFieldType.Checkbox)) html.Append(" placeholder=\"").Append(A(field.Placeholder)).Append('"');
        var autocomplete = field.Autocomplete ?? (field.Type == WebFieldType.Autocomplete ? "off" : null);
        if (autocomplete is not null) html.Append(" autocomplete=\"").Append(A(autocomplete)).Append('"');
        foreach (var (name, value) in field.InputAttributes)
            html.Append(' ').Append(A(name)).Append("=\"").Append(A(value)).Append('"');
    }

    private static void Length(StringBuilder html, WebFieldDefinition field)
    {
        if (field.MinLength is { } min) html.Append(" minlength=\"").Append(min.ToString(CultureInfo.InvariantCulture)).Append('"');
        if (field.MaxLength is { } max) html.Append(" maxlength=\"").Append(max.ToString(CultureInfo.InvariantCulture)).Append('"');
    }

    private void AppendLabel(StringBuilder html, WebFieldDefinition field, string id)
    {
        html.Append("<label class=\"").Append(A(Classes.Label)).Append("\" for=\"").Append(A(id)).Append("\">").Append(A(field.Label));
        if (field.Required) html.Append(" <span aria-hidden=\"true\">*</span>");
        html.Append("</label>");
    }

    private void AppendError(StringBuilder html, string key, string errorId, IReadOnlyList<string> messages) =>
        html.Append("<div class=\"").Append(A(Classes.Error)).Append("\" id=\"").Append(A(errorId)).Append("\" data-wl-error=\"").Append(A(key)).Append("\">")
            .Append(A(string.Join(' ', messages))).Append("</div>");

    private static void AppendShowWhen(StringBuilder html, WebFieldDefinition field, string key, Func<string, bool> visible)
    {
        if (field.ShowWhen is null)
            return;
        html.Append(" data-wl-show-when=\"").Append(A(JsonSerializer.Serialize(new { field = field.ShowWhen.Field, values = field.ShowWhen.Values }, WebJson.Default))).Append('"');
        if (!visible(key))
            html.Append(" hidden");
    }

    private async ValueTask AppendRepeaterAsync(StringBuilder html, WebFormRenderRequest request, WebFieldDefinition field, string key, string formId,
        WebFormErrors errors, Func<string, bool> visible, CancellationToken cancellationToken)
    {
        var indexes = new SortedSet<int>();
        foreach (var name in request.Values.Keys)
        {
            if (name.StartsWith(key + "[", StringComparison.Ordinal) && int.TryParse(name.AsSpan(key.Length + 1, Math.Max(0, name.IndexOf(']', key.Length) - key.Length - 1)), out var i))
                indexes.Add(i);
        }
        for (var i = 0; indexes.Count < field.RepeaterMin; i++)
            indexes.Add(i);

        html.Append("<fieldset class=\"").Append(A(Classes.Fieldset)).Append("\" data-wl-field=\"").Append(A(key)).Append("\" data-wl-repeater=\"").Append(A(key))
            .Append("\" data-wl-repeater-min=\"").Append(field.RepeaterMin.ToString(CultureInfo.InvariantCulture))
            .Append("\" data-wl-repeater-max=\"").Append(field.RepeaterMax.ToString(CultureInfo.InvariantCulture)).Append('"');
        AppendShowWhen(html, field, key, visible);
        html.Append("><legend class=\"").Append(A(Classes.Legend)).Append("\">").Append(A(field.Label)).Append("</legend><div data-wl-repeater-items>");
        foreach (var index in indexes.Take(field.RepeaterMax))
            await AppendRepeaterItemAsync(html, request, field, $"{key}[{index}].", formId, errors, visible, cancellationToken).ConfigureAwait(false);
        html.Append("</div><template data-wl-repeater-template>");
        await AppendRepeaterItemAsync(html, new WebFormRenderRequest { Definition = request.Definition, ProviderOptions = request.ProviderOptions },
            field, $"{key}[__index__].", formId, new WebFormErrors(), _ => true, cancellationToken).ConfigureAwait(false);
        html.Append("</template><button type=\"button\" class=\"").Append(A(Classes.Secondary)).Append("\" data-wl-repeater-add>Add</button>");
        var repeaterErrors = errors.For(key);
        if (repeaterErrors.Count > 0)
            AppendError(html, key, "wl-err-" + formId + "-" + IdPart(key), repeaterErrors);
        html.Append("</fieldset>\n");
    }

    private async ValueTask AppendRepeaterItemAsync(StringBuilder html, WebFormRenderRequest request, WebFieldDefinition field, string prefix, string formId,
        WebFormErrors errors, Func<string, bool> visible, CancellationToken cancellationToken)
    {
        html.Append("<div class=\"").Append(A(Classes.RepeaterItem)).Append("\" data-wl-repeater-item>");
        foreach (var child in field.Repeater!.Fields)
            await AppendFieldAsync(html, request, child, prefix, formId, errors, visible, cancellationToken).ConfigureAwait(false);
        html.Append("<button type=\"button\" class=\"").Append(A(Classes.Secondary)).Append("\" data-wl-repeater-remove>Remove</button></div>");
    }

    /// <summary>A theme override (<c>forms/fields/{type}.html</c>) for the field's inner markup, or null.</summary>
    private async ValueTask<string?> ThemedAsync(WebFormRenderRequest request, WebFieldDefinition field, string key, string id, StringValues values,
        IReadOnlyList<string> errors, string control, string errorId, CancellationToken cancellationToken)
    {
        var templates = _services.Templates;
        var path = "forms/fields/" + field.Type.ToString().ToLowerInvariant();
        if (templates is null || !templates.Exists(path))
            return null;
        var model = new Dictionary<string, object?>
        {
            ["name"] = key,
            ["id"] = id,
            ["label"] = field.Label,
            ["help"] = field.Help,
            ["required"] = field.Required,
            ["value"] = field.Type == WebFieldType.Password ? "" : values.Count > 0 ? values[0] : "",
            ["errors"] = errors,
            ["errorId"] = errorId,
            ["control"] = new WebHtml(control)
        };
        return await templates.RenderAsync(new TemplateRenderRequest { TemplatePath = path, Model = model, HttpContext = request.HttpContext, IgnoreLayout = true }, cancellationToken).ConfigureAwait(false);
    }
}

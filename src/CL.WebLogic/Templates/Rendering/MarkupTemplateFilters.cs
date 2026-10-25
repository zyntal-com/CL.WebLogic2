using System.Text.Encodings.Web;
using System.Text.Json;
using CL.WebLogic.Runtime.Services;
using CL.WebLogic.Templates.Escaping;

namespace CL.WebLogic.Templates.Rendering;

/// <summary>Context-aware output filters: nl2br, json, js, attr, css, markdown, sanitize.</summary>
internal static class MarkupTemplateFilters
{
    public static void Register(IDictionary<string, Func<object?, string?, TemplateRenderContext, object?>> filters)
    {
        // Encode first, then insert the line breaks — the result is safe HTML.
        filters["nl2br"] = (v, _, _) => new WebHtml(string.Join("<br>",
            TemplateValues.ToText(v).Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Select(TemplateEncoder.Html.Encode)));

        // JSON with <, >, &, ' and " escaped as \uXXXX: safe inside <script> and data attributes.
        filters["json"] = (v, _, _) => new EncodedValue(
            v is WebHtml or EncodedValue ? JsonSerializer.Serialize(TemplateValues.ToText(v), WebJson.Default) : JsonSerializer.Serialize(v, v?.GetType() ?? typeof(object), WebJson.Default),
            TemplateEncodingContext.Script);

        // Content for a JS string literal written as '…' or "…".
        filters["js"] = (v, _, _) => new EncodedValue(JavaScriptEncoder.Default.Encode(TemplateValues.ToText(v)), TemplateEncodingContext.Script);

        filters["attr"] = (v, _, _) => new EncodedValue(TemplateEncoder.Html.Encode(TemplateValues.ToText(v)), TemplateEncodingContext.Attribute);
        filters["css"] = (v, _, _) => new EncodedValue(TemplateEncoder.SanitizeCssValue(TemplateValues.ToText(v)), TemplateEncodingContext.Style);
        filters["markdown"] = (v, _, ctx) => ctx.Services.Markdown.Render(TemplateValues.ToText(v));
        filters["sanitize"] = (v, _, ctx) => ctx.Services.Sanitizer.Sanitize(TemplateValues.ToText(v));
    }
}

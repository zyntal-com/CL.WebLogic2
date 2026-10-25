using CL.WebLogic.Templates.Ast;
using CL.WebLogic.Templates.Escaping;

namespace CL.WebLogic.Templates.Parsing;

/// <summary>
/// Which template values may appear in which markup context. Violations are parse errors, so
/// an unsafe template fails at load time instead of producing an injectable page.
/// </summary>
internal static class TemplateContextRules
{
    /// <summary>Returns an error message when a value token is not allowed in <paramref name="context"/>, else null.</summary>
    public static string? ValidateToken(TemplateEncodingContext context, IReadOnlyList<TemplateFilter> filters, bool raw, bool inScriptString = false)
    {
        if (raw)
            return null;

        var last = filters.Count > 0 ? filters[^1].Name : string.Empty;
        return context switch
        {
            TemplateEncodingContext.TagBody =>
                "Template values cannot be written inside a tag outside an attribute value.",
            TemplateEncodingContext.UnquotedAttribute =>
                "Template values in attributes must be inside a quoted attribute value.",
            TemplateEncodingContext.EventHandler =>
                "Template values cannot be written into inline event handlers (on*). Use data-* attributes and a script module.",
            TemplateEncodingContext.Script when !IsOneOf(last, "json", "js") =>
                "Template values inside <script> must end with |json or |js.",
            // |js escapes for a string literal; outside quotes the value would be code.
            TemplateEncodingContext.Script when IsOneOf(last, "js") && !inScriptString =>
                "|js is only safe inside a quoted JavaScript string; outside quotes use |json.",
            TemplateEncodingContext.Style when !IsOneOf(last, "css") =>
                "Template values inside <style> must end with |css.",
            _ => null
        };
    }

    /// <summary>Returns an error when a URL/text-producing directive (asset, t) is not allowed here, else null.</summary>
    public static string? ValidateTextDirective(TemplateEncodingContext context, string directive) => context switch
    {
        TemplateEncodingContext.Html or TemplateEncodingContext.Attribute or TemplateEncodingContext.UrlAttribute => null,
        _ => $"{{{directive}:…}} can only be used in element text or a quoted attribute value."
    };

    /// <summary>Returns an error when a markup-producing directive is not in element text, else null.</summary>
    public static string? ValidateMarkupDirective(TemplateEncodingContext context, string directive) =>
        context == TemplateEncodingContext.Html ? null : $"{{{directive}}} produces markup and can only be used in element text.";

    private static bool IsOneOf(string value, params string[] names) =>
        names.Any(n => string.Equals(n, value, StringComparison.OrdinalIgnoreCase));
}

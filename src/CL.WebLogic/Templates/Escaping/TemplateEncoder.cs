using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using CL.WebLogic.Content.Services;

namespace CL.WebLogic.Templates.Escaping;

/// <summary>Encodes text for a <see cref="TemplateEncodingContext"/>.</summary>
public static class TemplateEncoder
{
    /// <summary>HTML encoder that leaves non-ASCII letters readable (still encodes &lt; &gt; &amp; quotes and controls).</summary>
    public static HtmlEncoder Html { get; } = HtmlEncoder.Create(UnicodeRanges.All);

    private static readonly char[] LineChars = ['\n', '\t'];

    /// <summary>HTML-encodes element text, keeping line feeds and tabs as-is (they are safe in text).</summary>
    public static string HtmlText(string text)
    {
        if (string.IsNullOrEmpty(text) || text.IndexOfAny(LineChars) < 0)
            return Html.Encode(text ?? string.Empty);

        var builder = new StringBuilder(text.Length + 16);
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] is not ('\n' or '\t'))
                continue;
            builder.Append(Html.Encode(text.Substring(start, i - start))).Append(text[i]);
            start = i + 1;
        }
        return builder.Append(Html.Encode(text.Substring(start))).ToString();
    }

    /// <summary>Replacement written for URL values with a disallowed scheme.</summary>
    public const string BlockedUrl = "about:blank#blocked";

    /// <summary>Encodes <paramref name="text"/> for <paramref name="context"/>.</summary>
    public static string Encode(string? text, TemplateEncodingContext context)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        return context switch
        {
            TemplateEncodingContext.Plain => text,
            TemplateEncodingContext.UrlAttribute => Html.Encode(UrlSchemePolicy.Default.IsAllowed(text) ? text : BlockedUrl),
            TemplateEncodingContext.ImageUrlAttribute => Html.Encode(UrlSchemePolicy.WithDataImages.IsAllowed(text) ? text : BlockedUrl),
            TemplateEncodingContext.StyleAttribute or TemplateEncodingContext.Style => Html.Encode(SanitizeCssValue(text)),
            TemplateEncodingContext.Script => JavaScriptEncoder.Default.Encode(text),
            TemplateEncodingContext.Html => HtmlText(text),
            _ => Html.Encode(text)
        };
    }

    /// <summary>
    /// Keeps a CSS value only when it consists of safe characters (letters, digits, space and
    /// <c># % . , ( ) + - _ /</c>) and contains no function-like constructs that load resources.
    /// Anything else becomes empty.
    /// </summary>
    public static string SanitizeCssValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (char.IsLetterOrDigit(c) || c is ' ' or '#' or '%' or '.' or ',' or '(' or ')' or '+' or '-' or '_' or '/')
                builder.Append(c);
            else
                return string.Empty;
        }

        var result = builder.ToString();
        return result.Contains("url(", StringComparison.OrdinalIgnoreCase)
            || result.Contains("expression(", StringComparison.OrdinalIgnoreCase)
            || result.Contains("image-set(", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : result;
    }
}

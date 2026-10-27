using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using CL.WebLogic.Content.Models;
using CL.WebLogic.Templates.Escaping;

namespace CL.WebLogic.Content.Services;

/// <summary>
/// Allowlist HTML sanitizer. Input is tokenized with <see cref="HtmlTokenizer"/> and re-serialized
/// from scratch: only allowlisted elements and attributes survive, every text and attribute value
/// is re-encoded, URL attributes must pass the scheme policy, and the output is always balanced.
/// Because nothing from the input is copied verbatim, parser differentials cannot smuggle markup.
/// </summary>
public sealed class WebHtmlSanitizer : IWebHtmlSanitizer
{
    private static readonly HashSet<string> VoidElements = new(StringComparer.OrdinalIgnoreCase) { "br", "hr", "img", "wbr" };
    private static readonly HtmlEncoder Encoder = HtmlEncoder.Create(UnicodeRanges.All);

    private readonly WebHtmlSanitizerOptions _options;

    /// <summary>Creates a sanitizer with <paramref name="options"/> (defaults when null).</summary>
    public WebHtmlSanitizer(WebHtmlSanitizerOptions? options = null) => _options = options ?? WebHtmlSanitizerOptions.Default;

    /// <inheritdoc/>
    public WebHtml Sanitize(string? html)
    {
        if (string.IsNullOrEmpty(html))
            return WebHtml.Empty;

        var output = new StringBuilder(html.Length);
        var open = new List<string>();
        string? dropping = null;
        var dropDepth = 0;

        foreach (var token in HtmlTokenizer.Tokenize(html))
        {
            if (dropping is not null)
            {
                if (token.Kind == HtmlTokenKind.StartTag && token.Name == dropping && !token.SelfClosing) dropDepth++;
                else if (token.Kind == HtmlTokenKind.EndTag && token.Name == dropping && --dropDepth == 0) dropping = null;
                continue;
            }

            switch (token.Kind)
            {
                case HtmlTokenKind.Text:
                    output.Append(TemplateEncoder.HtmlText(token.Text));
                    break;

                case HtmlTokenKind.StartTag when token.Name == "iframe" && AllowedFrame(token) is { } src:
                    WriteFrame(output, token, src);
                    if (!token.SelfClosing)
                    {
                        dropping = "iframe"; // its fallback text and end tag
                        dropDepth = 1;
                    }
                    break;

                case HtmlTokenKind.StartTag when _options.DroppedElements.Contains(token.Name):
                    if (!token.SelfClosing && !VoidElements.Contains(token.Name))
                    {
                        dropping = token.Name;
                        dropDepth = 1;
                    }
                    break;

                case HtmlTokenKind.StartTag when _options.AllowedElements.Contains(token.Name) && open.Count < _options.MaxDepth:
                    WriteStartTag(output, token);
                    if (!VoidElements.Contains(token.Name))
                        open.Add(token.Name);
                    break;

                case HtmlTokenKind.EndTag:
                    var index = open.LastIndexOf(token.Name);
                    if (index < 0)
                        break;
                    for (var i = open.Count - 1; i >= index; i--)
                        output.Append("</").Append(open[i]).Append('>');
                    open.RemoveRange(index, open.Count - index);
                    break;
            }
        }

        for (var i = open.Count - 1; i >= 0; i--)
            output.Append("</").Append(open[i]).Append('>');

        return new WebHtml(output.ToString());
    }

    private void WriteStartTag(StringBuilder output, HtmlToken token)
    {
        output.Append('<').Append(token.Name);
        _options.ElementAttributes.TryGetValue(token.Name, out var elementAttributes);
        var isLink = token.Name == "a";

        foreach (var (name, value) in token.Attributes)
        {
            if (name == "style")
            {
                if (CleanStyle(value) is { Length: > 0 } style)
                    output.Append(" style=\"").Append(Encoder.Encode(style)).Append('"');
                continue;
            }
            if (!_options.GlobalAttributes.Contains(name) && elementAttributes?.Contains(name) != true)
                continue;
            if (_options.UrlAttributes.Contains(name) && !_options.UrlPolicy.IsAllowed(value))
                continue;
            if (name == "target" && value != "_blank")
                continue;
            if (name is "width" or "height" or "colspan" or "rowspan" or "start" or "value" && !IsSmallNumber(value))
                continue;

            output.Append(' ').Append(name).Append("=\"").Append(Encoder.Encode(value)).Append('"');
        }

        if (isLink && _options.LinkRel is { Length: > 0 } rel)
            output.Append(" rel=\"").Append(Encoder.Encode(rel)).Append('"');

        output.Append('>');
    }

    /// <summary>The iframe's <c>src</c> when it matches an allowed frame source, otherwise null.</summary>
    private string? AllowedFrame(HtmlToken token)
    {
        if (_options.AllowedFrameSources.Count == 0)
            return null;
        var src = token.Attributes.FirstOrDefault(a => a.Key == "src").Value;
        return src is not null && _options.AllowedFrameSources.Any(f => f.Matches(src)) ? new Uri(src.Trim()).AbsoluteUri : null;
    }

    /// <summary>Rebuilds an allowed iframe: src, size, title, loading, global attributes and the fixed sandbox.</summary>
    private void WriteFrame(StringBuilder output, HtmlToken token, string src)
    {
        output.Append("<iframe src=\"").Append(Encoder.Encode(src)).Append('"');
        foreach (var (name, value) in token.Attributes)
        {
            var keep = name switch
            {
                "width" or "height" => IsSmallNumber(value),
                "title" => true,
                "loading" => value is "lazy" or "eager",
                "src" or "style" => false,
                _ => _options.GlobalAttributes.Contains(name)
            };
            if (keep)
                output.Append(' ').Append(name).Append("=\"").Append(Encoder.Encode(value)).Append('"');
        }
        if (CleanStyle(token.Attributes.FirstOrDefault(a => a.Key == "style").Value) is { Length: > 0 } style)
            output.Append(" style=\"").Append(Encoder.Encode(style)).Append('"');
        output.Append(" sandbox=\"").Append(Encoder.Encode(_options.FrameSandbox)).Append('"');
        if (_options.FrameAllow is { Length: > 0 } allow)
            output.Append(" allow=\"").Append(Encoder.Encode(allow)).Append('"');
        output.Append(" allowfullscreen referrerpolicy=\"strict-origin-when-cross-origin\"></iframe>");
    }

    /// <summary>
    /// The allowed declarations of a <c>style</c> value, rebuilt as <c>name: value; …</c>, or null.
    /// Anything that could load resources or escape the declaration is refused before the per-property
    /// check: only letters, digits, space and <c># % . , ( ) - /</c> pass, no comments, and the only
    /// functions are the colour functions.
    /// </summary>
    private string? CleanStyle(string? style)
    {
        if (string.IsNullOrWhiteSpace(style) || _options.AllowedStyleProperties.Count == 0 || style.Length > 1024)
            return null;

        var kept = new List<string>();
        foreach (var declaration in style.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var colon = declaration.IndexOf(':');
            if (colon <= 0)
                continue;
            var name = declaration[..colon].Trim().ToLowerInvariant();
            var value = declaration[(colon + 1)..].Trim();
            if (value.EndsWith("!important", StringComparison.OrdinalIgnoreCase))
                value = value[..^"!important".Length].TrimEnd();
            if (value.Length == 0 || !IsPlainCssValue(value))
                continue;
            if (_options.AllowedStyleProperties.TryGetValue(name, out var check) && check(value))
                kept.Add(name + ": " + value);
        }
        return kept.Count == 0 ? null : string.Join("; ", kept);
    }

    private static bool IsPlainCssValue(string value)
    {
        if (value.Contains("/*", StringComparison.Ordinal))
            return false;
        foreach (var c in value)
        {
            if (!(char.IsAsciiLetterOrDigit(c) || c is ' ' or '#' or '%' or '.' or ',' or '(' or ')' or '-' or '/'))
                return false;
        }
        var paren = value.IndexOf('(');
        if (paren < 0)
            return true;
        // Only colour functions: rgb(, rgba(, hsl(, hsla( — nothing else may open a parenthesis.
        var functionStart = paren;
        while (functionStart > 0 && char.IsAsciiLetter(value[functionStart - 1]))
            functionStart--;
        var function = value[functionStart..paren].ToLowerInvariant();
        return function is "rgb" or "rgba" or "hsl" or "hsla" && value.IndexOf('(', paren + 1) < 0;
    }

    private static bool IsSmallNumber(string value) =>
        value.Length is > 0 and <= 5 && value.All(char.IsAsciiDigit);
}

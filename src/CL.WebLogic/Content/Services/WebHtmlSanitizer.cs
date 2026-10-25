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

    private static bool IsSmallNumber(string value) =>
        value.Length is > 0 and <= 5 && value.All(char.IsAsciiDigit);
}

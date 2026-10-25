using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using CL.WebLogic.Content.Models;
using CL.WebLogic.Templates.Escaping;

namespace CL.WebLogic.Content.Services;

/// <summary>
/// Markdown to HTML for a practical CommonMark + GFM subset: ATX headings, paragraphs, hard
/// breaks, emphasis / strong / strikethrough, code spans, fenced and indented code, block
/// quotes, nested lists, tables, thematic breaks, links, images and autolinks. Raw HTML in the
/// input is never passed through — it is written as text — and every URL is scheme-checked.
/// </summary>
public sealed class WebMarkdownRenderer : IWebMarkdownRenderer
{
    internal static readonly HtmlEncoder Encoder = HtmlEncoder.Create(UnicodeRanges.All);

    private readonly WebMarkdownOptions _options;

    /// <summary>Creates a renderer with <paramref name="options"/> (defaults when null).</summary>
    public WebMarkdownRenderer(WebMarkdownOptions? options = null) => _options = options ?? WebMarkdownOptions.Default;

    /// <inheritdoc/>
    public WebHtml Render(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return WebHtml.Empty;

        if (markdown.Length > _options.MaxInputLength)
            return new WebHtml("<p>" + Encoder.Encode(markdown) + "</p>");

        var lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\t", "    ").Split('\n');
        var output = new StringBuilder(markdown.Length + markdown.Length / 4);
        new MarkdownBlockRenderer(_options, new MarkdownInlineRenderer(_options)).Render(lines, output, depth: 0, tight: false);
        return new WebHtml(output.ToString().TrimEnd('\n'));
    }
}

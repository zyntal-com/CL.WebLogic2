using System.Text;
using CL.WebLogic.Templates.Escaping;
using CL.WebLogic.Content.Models;

namespace CL.WebLogic.Content.Services;

/// <summary>Block-level Markdown: splits lines into blocks and renders each.</summary>
internal sealed partial class MarkdownBlockRenderer
{
    private const int MaxDepth = 16;

    private readonly WebMarkdownOptions _options;
    private readonly MarkdownInlineRenderer _inline;

    public MarkdownBlockRenderer(WebMarkdownOptions options, MarkdownInlineRenderer inline)
    {
        _options = options;
        _inline = inline;
    }

    /// <summary>Renders <paramref name="lines"/>; in a <paramref name="tight"/> list item paragraphs are not wrapped in &lt;p&gt;.</summary>
    public void Render(IReadOnlyList<string> lines, StringBuilder output, int depth, bool tight)
    {
        var i = 0;
        while (i < lines.Count)
        {
            var line = lines[i];
            if (IsBlank(line)) { i++; continue; }

            if (depth < MaxDepth)
            {
                if (TryFence(line, out var fence, out var info)) { i = RenderFence(lines, i, fence, info, output); continue; }
                if (Indent(line) >= 4) { i = RenderIndentedCode(lines, i, output); continue; }
                if (TryHeading(line, out var level, out var text)) { RenderHeading(level, text, output); i++; continue; }
                if (IsThematicBreak(line)) { output.Append("<hr>\n"); i++; continue; }
                if (IsQuote(line)) { i = RenderQuote(lines, i, output, depth); continue; }
                if (TryListMarker(line, out var marker)) { i = RenderList(lines, i, marker, output, depth); continue; }
                if (IsTableStart(lines, i)) { i = RenderTable(lines, i, output); continue; }
            }

            i = RenderParagraph(lines, i, output, tight);
        }
    }

    private int RenderParagraph(IReadOnlyList<string> lines, int start, StringBuilder output, bool tight)
    {
        var i = start;
        var text = new StringBuilder();
        while (i < lines.Count && !IsBlank(lines[i]) && (i == start || !InterruptsParagraph(lines[i])))
        {
            if (text.Length > 0) text.Append('\n');
            text.Append(lines[i].TrimStart());
            i++;
        }

        var content = text.ToString().TrimEnd();
        if (!tight) output.Append("<p>");
        _inline.Render(content, output, 0);
        output.Append(tight ? "\n" : "</p>\n");
        return i;
    }

    private static bool InterruptsParagraph(string line) =>
        TryFence(line, out _, out _) || TryHeading(line, out _, out _) || IsThematicBreak(line) || IsQuote(line)
        || (TryListMarker(line, out var marker) && (!marker.Ordered || marker.Start == 1) && !IsBlank(line.Substring(Math.Min(line.Length, marker.ContentOffset))));

    private void RenderHeading(int level, string text, StringBuilder output)
    {
        output.Append("<h").Append(level);
        if (_options.GenerateHeadingIds)
        {
            var slug = MarkdownText.Slug(text);
            if (slug.Length > 0)
                output.Append(" id=\"").Append(WebMarkdownRenderer.Encoder.Encode(_options.HeadingIdPrefix + slug)).Append('"');
        }
        output.Append('>');
        _inline.Render(text, output, 0);
        output.Append("</h").Append(level).Append(">\n");
    }

    private static int RenderFence(IReadOnlyList<string> lines, int start, string fence, string info, StringBuilder output)
    {
        var indent = Indent(lines[start]);
        var i = start + 1;
        var code = new StringBuilder();
        while (i < lines.Count)
        {
            var trimmed = lines[i].TrimStart();
            if (Indent(lines[i]) < 4 && trimmed.StartsWith(fence, StringComparison.Ordinal) && trimmed.TrimEnd().All(c => c == fence[0]))
            {
                i++;
                break;
            }
            code.Append(StripIndent(lines[i], indent)).Append('\n');
            i++;
        }

        output.Append("<pre><code");
        var language = info.Split(' ', 2)[0];
        if (language.Length > 0)
            output.Append(" class=\"language-").Append(WebMarkdownRenderer.Encoder.Encode(language)).Append('"');
        output.Append('>').Append(TemplateEncoder.HtmlText(code.ToString())).Append("</code></pre>\n");
        return i;
    }

    private static int RenderIndentedCode(IReadOnlyList<string> lines, int start, StringBuilder output)
    {
        var i = start;
        var buffer = new List<string>();
        while (i < lines.Count && (IsBlank(lines[i]) || Indent(lines[i]) >= 4))
            buffer.Add(StripIndent(lines[i++], 4));
        while (buffer.Count > 0 && IsBlank(buffer[^1]))
        {
            buffer.RemoveAt(buffer.Count - 1);
        }

        output.Append("<pre><code>").Append(TemplateEncoder.HtmlText(string.Join('\n', buffer) + "\n")).Append("</code></pre>\n");
        return i;
    }

    private int RenderQuote(IReadOnlyList<string> lines, int start, StringBuilder output, int depth)
    {
        var i = start;
        var inner = new List<string>();
        while (i < lines.Count && !IsBlank(lines[i]))
        {
            var line = lines[i];
            if (IsQuote(line))
            {
                var content = line.TrimStart().Substring(1);
                inner.Add(content.StartsWith(' ') ? content.Substring(1) : content);
            }
            else if (inner.Count > 0 && !IsBlank(inner[^1]) && !InterruptsParagraph(line))
            {
                inner.Add(line); // lazy continuation
            }
            else
            {
                break;
            }
            i++;
        }

        output.Append("<blockquote>\n");
        Render(inner, output, depth + 1, tight: false);
        output.Append("</blockquote>\n");
        return i;
    }
}

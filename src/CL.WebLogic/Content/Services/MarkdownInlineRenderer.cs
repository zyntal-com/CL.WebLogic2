using System.Net;
using System.Text;
using CL.WebLogic.Content.Models;

namespace CL.WebLogic.Content.Services;

/// <summary>
/// Inline Markdown: escapes, code spans, emphasis, strong, strikethrough, links, images,
/// autolinks and hard breaks. All text is encoded; raw HTML is written as text.
/// </summary>
internal sealed partial class MarkdownInlineRenderer
{
    private const int MaxDepth = 16;

    private readonly WebMarkdownOptions _options;

    public MarkdownInlineRenderer(WebMarkdownOptions options) => _options = options;

    /// <summary>Renders <paramref name="text"/> into <paramref name="output"/>.</summary>
    public void Render(string text, StringBuilder output, int depth)
    {
        var plain = new StringBuilder();
        var i = 0;

        while (i < text.Length)
        {
            var c = text[i];

            if (c == '\\' && i + 1 < text.Length)
            {
                if (text[i + 1] == '\n') { Flush(); output.Append("<br>\n"); i += 2; continue; }
                if (MarkdownText.IsAsciiPunctuation(text[i + 1])) { plain.Append(text[i + 1]); i += 2; continue; }
            }

            if (c == '\n')
            {
                var hard = plain.Length >= 2 && plain[^1] == ' ' && plain[^2] == ' ';
                while (plain.Length > 0 && plain[^1] == ' ') plain.Length--;
                Flush();
                output.Append(hard ? "<br>\n" : "\n");
                i++;
                continue;
            }

            if (depth < MaxDepth)
            {
                var consumed = c switch
                {
                    '`' => TryCodeSpan(text, i, output, Flush),
                    '*' or '_' => TryEmphasis(text, i, output, depth, Flush),
                    '~' => TryStrikethrough(text, i, output, depth, Flush),
                    '!' when i + 1 < text.Length && text[i + 1] == '[' => TryLink(text, i + 1, image: true, output, depth, Flush),
                    '[' => TryLink(text, i, image: false, output, depth, Flush),
                    '<' => TryAutolink(text, i, output, Flush),
                    'h' or 'w' or 'H' or 'W' when _options.AutoLinkBareUrls => TryBareUrl(text, i, output, Flush),
                    _ => 0
                };

                if (consumed > 0)
                {
                    i += consumed;
                    continue;
                }
            }

            if (c == '&' && TryEntity(text, i, out var entityLength, out var decoded))
            {
                plain.Append(decoded);
                i += entityLength;
                continue;
            }

            plain.Append(c);
            i++;
        }

        Flush();

        void Flush()
        {
            if (plain.Length == 0) return;
            output.Append(Templates.Escaping.TemplateEncoder.HtmlText(plain.ToString()));
            plain.Clear();
        }
    }

    /// <summary>
    /// How far any delimiter search looks ahead. Without a bound, input such as many unmatched
    /// <c>[</c> or <c>*</c> makes rendering quadratic; spans longer than this stay literal.
    /// </summary>
    internal const int MaxScan = 4096;

    /// <summary>The exclusive end of a bounded search starting at <paramref name="from"/>.</summary>
    internal static int ScanEnd(string text, int from) => (int)Math.Min(text.Length, (long)from + MaxScan);

    /// <summary><see cref="string.IndexOf(string, int, StringComparison)"/> within the scan window.</summary>
    internal static int BoundedIndexOf(string text, string value, int from)
    {
        if (from >= text.Length) return -1;
        return text.IndexOf(value, from, ScanEnd(text, from) - from, StringComparison.Ordinal);
    }

    /// <summary><see cref="string.IndexOf(char, int)"/> within the scan window.</summary>
    internal static int BoundedIndexOf(string text, char value, int from)
    {
        if (from >= text.Length) return -1;
        return text.IndexOf(value, from, ScanEnd(text, from) - from);
    }

    private static int TryCodeSpan(string text, int start, StringBuilder output, Action flush)
    {
        var run = RunLength(text, start, '`');
        var search = start + run;
        var limit = ScanEnd(text, search);
        while (search < limit)
        {
            var close = BoundedIndexOf(text, '`', search);
            if (close >= limit) break;
            if (close < 0) break;
            var closeRun = RunLength(text, close, '`');
            if (closeRun == run)
            {
                var code = text.Substring(start + run, close - start - run).Replace('\n', ' ');
                if (code.Length >= 2 && code[0] == ' ' && code[^1] == ' ' && code.Trim().Length > 0)
                    code = code.Substring(1, code.Length - 2);
                flush();
                output.Append("<code>").Append(WebMarkdownRenderer.Encoder.Encode(code)).Append("</code>");
                return close + closeRun - start;
            }
            search = close + closeRun;
        }

        // No matching closer: the whole backtick run is literal.
        flush();
        output.Append(new string('`', run));
        return run;
    }

    private int TryEmphasis(string text, int start, StringBuilder output, int depth, Action flush)
    {
        var c = text[start];
        var run = RunLength(text, start, c);
        var openEnd = start + run;

        // Opener must be left-flanking; '_' must not be intraword.
        if (openEnd >= text.Length || char.IsWhiteSpace(text[openEnd]))
            return 0;
        if (c == '_' && start > 0 && char.IsLetterOrDigit(text[start - 1]))
            return 0;

        var size = Math.Min(run, 3);
        for (var width = size; width >= 1; width--)
        {
            var close = FindCloser(text, start + width, c, width);
            if (close < 0)
                continue;

            flush();
            if (run > width)
                output.Append(new string(c, run - width)); // surplus delimiters are literal
            var (open, end) = width switch
            {
                3 => ("<em><strong>", "</strong></em>"),
                2 => ("<strong>", "</strong>"),
                _ => ("<em>", "</em>")
            };
            output.Append(open);
            Render(text.Substring(start + run, close - start - run), output, depth + 1);
            output.Append(end);
            return close + width - start;
        }

        return 0;
    }

    private int TryStrikethrough(string text, int start, StringBuilder output, int depth, Action flush)
    {
        if (RunLength(text, start, '~') != 2 || start + 2 >= text.Length || char.IsWhiteSpace(text[start + 2]))
            return 0;

        var close = FindCloser(text, start + 2, '~', 2);
        if (close < 0)
            return 0;

        flush();
        output.Append("<del>");
        Render(text.Substring(start + 2, close - start - 2), output, depth + 1);
        output.Append("</del>");
        return close + 2 - start;
    }

    /// <summary>Finds a closing run of exactly <paramref name="width"/> delimiters, skipping code spans and escapes.</summary>
    private static int FindCloser(string text, int from, char c, int width)
    {
        var i = from;
        var limit = ScanEnd(text, from);
        while (i < limit)
        {
            var ch = text[i];
            if (ch == '\\') { i += 2; continue; }
            if (ch == '`')
            {
                var run = RunLength(text, i, '`');
                var close = BoundedIndexOf(text, new string('`', run), i + run);
                i = close < 0 ? i + run : close + run;
                continue;
            }
            if (ch == c)
            {
                var run = RunLength(text, i, c);
                var flanking = i > from && !char.IsWhiteSpace(text[i - 1]);
                var intraword = c == '_' && i + run < text.Length && char.IsLetterOrDigit(text[i + run]);
                if (run == width && flanking && !intraword)
                    return i;
                if (run > width && flanking && !intraword && width == 1 && run == 3)
                    return i + 2; // "*a **b***": the last delimiter closes the emphasis
                i += run;
                continue;
            }
            i++;
        }
        return -1;
    }

    private static int RunLength(string text, int start, char c)
    {
        var n = 0;
        while (start + n < text.Length && text[start + n] == c) n++;
        return n;
    }

    private static bool TryEntity(string text, int start, out int length, out string decoded)
    {
        length = 0;
        decoded = string.Empty;
        var semicolon = text.IndexOf(';', start, Math.Min(33, text.Length - start));
        if (semicolon < 0)
            return false;

        var candidate = text.Substring(start, semicolon - start + 1);
        if (!candidate.Skip(1).Take(candidate.Length - 2).All(ch => char.IsAsciiLetterOrDigit(ch) || ch == '#'))
            return false;

        var result = WebUtility.HtmlDecode(candidate);
        if (result == candidate)
            return false;

        length = candidate.Length;
        decoded = result;
        return true;
    }
}

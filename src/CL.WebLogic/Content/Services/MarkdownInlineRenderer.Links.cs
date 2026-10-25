using System.Text;
using System.Text.RegularExpressions;

namespace CL.WebLogic.Content.Services;

// Links, images, autolinks and bare URLs.
internal sealed partial class MarkdownInlineRenderer
{
    [GeneratedRegex(@"\G<([A-Za-z][A-Za-z0-9+.\-]{1,31}:[^\s<>]*)>", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 250)]
    private static partial Regex UriAutolink();

    [GeneratedRegex(@"\G<([A-Za-z0-9.!#$%&'*+/=?^_`{|}~\-]+@[A-Za-z0-9](?:[A-Za-z0-9\-]{0,61}[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9\-]{0,61}[A-Za-z0-9])?)+)>", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 250)]
    private static partial Regex EmailAutolink();

    private int TryLink(string text, int bracket, bool image, StringBuilder output, int depth, Action flush)
    {
        var labelEnd = FindLabelEnd(text, bracket);
        if (labelEnd < 0 || labelEnd + 1 >= text.Length || text[labelEnd + 1] != '(')
            return 0;

        if (!TryParseDestination(text, labelEnd + 2, out var url, out var title, out var end))
            return 0;

        var label = text.Substring(bracket + 1, labelEnd - bracket - 1);
        var start = image ? bracket - 1 : bracket;
        flush();

        var allowed = _options.UrlPolicy.IsAllowed(url);
        if (image)
        {
            if (allowed && _options.AllowImages)
            {
                output.Append("<img src=\"").Append(Encode(url)).Append("\" alt=\"").Append(Encode(PlainText(label))).Append('"');
                if (title is not null) output.Append(" title=\"").Append(Encode(title)).Append('"');
                output.Append('>');
            }
            else
            {
                output.Append(Encode(PlainText(label)));
            }
            return end - start;
        }

        if (!allowed)
        {
            Render(label, output, depth + 1);
            return end - start;
        }

        output.Append("<a href=\"").Append(Encode(url)).Append('"');
        if (title is not null) output.Append(" title=\"").Append(Encode(title)).Append('"');
        AppendRel(output);
        output.Append('>');
        Render(label, output, depth + 1);
        output.Append("</a>");
        return end - start;
    }

    private int TryAutolink(string text, int start, StringBuilder output, Action flush)
    {
        var uri = UriAutolink().Match(text, start);
        if (uri.Success && _options.UrlPolicy.IsAllowed(uri.Groups[1].Value))
        {
            flush();
            WriteLink(output, uri.Groups[1].Value, uri.Groups[1].Value);
            return uri.Length;
        }

        var email = EmailAutolink().Match(text, start);
        if (email.Success)
        {
            flush();
            WriteLink(output, "mailto:" + email.Groups[1].Value, email.Groups[1].Value);
            return email.Length;
        }

        return 0;
    }

    private int TryBareUrl(string text, int start, StringBuilder output, Action flush)
    {
        if (start > 0 && !char.IsWhiteSpace(text[start - 1]) && text[start - 1] is not ('(' or '*' or '_' or '~'))
            return 0;

        var isWww = string.Compare(text, start, "www.", 0, 4, StringComparison.OrdinalIgnoreCase) == 0;
        if (!isWww
            && string.Compare(text, start, "https://", 0, 8, StringComparison.OrdinalIgnoreCase) != 0
            && string.Compare(text, start, "http://", 0, 7, StringComparison.OrdinalIgnoreCase) != 0)
            return 0;

        var end = start;
        while (end < text.Length && !char.IsWhiteSpace(text[end]) && text[end] != '<') end++;
        while (end > start && text[end - 1] is '.' or ',' or ':' or ';' or '!' or '?' or '"' or '\'' or '*' or '_' or '~') end--;
        if (end > start && text[end - 1] == ')' && Count(text, start, end, '(') < Count(text, start, end, ')')) end--;

        var display = text.Substring(start, end - start);
        var href = isWww ? "https://" + display : display;
        if (display.Length <= (isWww ? 4 : 8) || !_options.UrlPolicy.IsAllowed(href))
            return 0;

        flush();
        WriteLink(output, href, display);
        return end - start;
    }

    private void WriteLink(StringBuilder output, string href, string display)
    {
        output.Append("<a href=\"").Append(Encode(href)).Append('"');
        AppendRel(output);
        output.Append('>').Append(Encode(display)).Append("</a>");
    }

    private void AppendRel(StringBuilder output)
    {
        if (!string.IsNullOrEmpty(_options.LinkRel))
            output.Append(" rel=\"").Append(Encode(_options.LinkRel)).Append('"');
    }

    private static int FindLabelEnd(string text, int bracket)
    {
        var depth = 0;
        var limit = ScanEnd(text, bracket);
        for (var i = bracket + 1; i < limit; i++)
        {
            switch (text[i])
            {
                case '\\': i++; break;
                case '`':
                    var run = RunLength(text, i, '`');
                    var close = BoundedIndexOf(text, new string('`', run), i + run);
                    i = (close < 0 ? i + run : close + run) - 1;
                    break;
                case '[': depth++; break;
                case ']' when depth == 0: return i;
                case ']': depth--; break;
            }
        }
        return -1;
    }

    private static bool TryParseDestination(string text, int start, out string url, out string? title, out int end)
    {
        url = string.Empty;
        title = null;
        end = 0;
        var i = start;
        while (i < text.Length && text[i] is ' ' or '\n') i++;

        if (i < text.Length && text[i] == '<')
        {
            var close = BoundedIndexOf(text, '>', i + 1);
            if (close < 0 || text.AsSpan(i + 1, close - i - 1).IndexOfAny('\n', '<') >= 0) return false;
            url = text.Substring(i + 1, close - i - 1);
            i = close + 1;
        }
        else
        {
            var parens = 0;
            var urlStart = i;
            var urlLimit = ScanEnd(text, i);
            while (i < urlLimit && !char.IsWhiteSpace(text[i]) && !char.IsControl(text[i]))
            {
                if (text[i] == '\\' && i + 1 < text.Length) { i += 2; continue; }
                if (text[i] == '(') parens++;
                else if (text[i] == ')') { if (parens == 0) break; parens--; }
                i++;
            }
            url = Unescape(text.Substring(urlStart, i - urlStart));
        }

        while (i < text.Length && text[i] is ' ' or '\n') i++;
        if (i < text.Length && text[i] is '"' or '\'' or '(')
        {
            var closer = text[i] == '(' ? ')' : text[i];
            var close = IndexOfUnescaped(text, closer, i + 1);
            if (close < 0) return false;
            title = Unescape(text.Substring(i + 1, close - i - 1));
            i = close + 1;
            while (i < text.Length && text[i] is ' ' or '\n') i++;
        }

        if (i >= text.Length || text[i] != ')')
            return false;

        end = i + 1;
        return true;
    }

    private static int IndexOfUnescaped(string text, char c, int from)
    {
        var limit = ScanEnd(text, from);
        for (var i = from; i < limit; i++)
        {
            if (text[i] == '\\') i++;
            else if (text[i] == c) return i;
        }
        return -1;
    }

    private static string PlainText(string label) =>
        Unescape(label).Replace("*", string.Empty, StringComparison.Ordinal).Replace("`", string.Empty, StringComparison.Ordinal);

    private static string Unescape(string value)
    {
        if (!value.Contains('\\'))
            return value;
        var builder = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '\\' && i + 1 < value.Length && MarkdownText.IsAsciiPunctuation(value[i + 1])) i++;
            builder.Append(value[i]);
        }
        return builder.ToString();
    }

    private static int Count(string text, int start, int end, char c)
    {
        var n = 0;
        for (var i = start; i < end; i++) if (text[i] == c) n++;
        return n;
    }

    private static string Encode(string value) => WebMarkdownRenderer.Encoder.Encode(value);
}

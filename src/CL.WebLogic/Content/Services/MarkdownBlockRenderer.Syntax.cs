namespace CL.WebLogic.Content.Services;

// Line classification helpers.
internal sealed partial class MarkdownBlockRenderer
{
    private static bool IsBlank(string line) => string.IsNullOrWhiteSpace(line);

    private static int Indent(string line)
    {
        var n = 0;
        while (n < line.Length && line[n] == ' ') n++;
        return n;
    }

    private static string StripIndent(string line, int count)
    {
        var n = 0;
        while (n < count && n < line.Length && line[n] == ' ') n++;
        return line.Substring(n);
    }

    private static bool IsQuote(string line) => Indent(line) < 4 && line.TrimStart().StartsWith('>');

    private static bool TryFence(string line, out string fence, out string info)
    {
        fence = string.Empty;
        info = string.Empty;
        if (Indent(line) >= 4)
            return false;

        var t = line.TrimStart();
        if (t.Length < 3 || (t[0] != '`' && t[0] != '~'))
            return false;

        var n = 0;
        while (n < t.Length && t[n] == t[0]) n++;
        if (n < 3)
            return false;

        info = t.Substring(n).Trim();
        if (t[0] == '`' && info.Contains('`'))
            return false;

        fence = new string(t[0], n);
        return true;
    }

    private static bool TryHeading(string line, out int level, out string text)
    {
        level = 0;
        text = string.Empty;
        if (Indent(line) >= 4)
            return false;

        var t = line.TrimStart();
        while (level < t.Length && t[level] == '#') level++;
        if (level is 0 or > 6 || (level < t.Length && t[level] != ' '))
            return false;

        text = t.Substring(level).Trim();
        // Optional closing sequence: "## Title ##".
        var closing = text.Length;
        while (closing > 0 && text[closing - 1] == '#') closing--;
        if (closing < text.Length && (closing == 0 || text[closing - 1] == ' '))
            text = text.Substring(0, closing).TrimEnd();
        return true;
    }

    private static bool IsThematicBreak(string line)
    {
        if (Indent(line) >= 4)
            return false;
        var t = line.Replace(" ", string.Empty, StringComparison.Ordinal);
        return t.Length >= 3 && (t[0] is '-' or '*' or '_') && t.All(c => c == t[0]);
    }

    private static bool TryListMarker(string line, out ListMarker marker)
    {
        marker = default;
        var indent = Indent(line);
        if (indent >= 4 || indent >= line.Length)
            return false;

        var c = line[indent];
        if (c is '-' or '*' or '+')
        {
            var after = indent + 1;
            if (after < line.Length && line[after] != ' ')
                return false;
            marker = new ListMarker(false, 1, c, ContentOffset(line, after), indent);
            return true;
        }

        var digits = indent;
        while (digits < line.Length && char.IsAsciiDigit(line[digits]) && digits - indent < 9) digits++;
        if (digits == indent || digits >= line.Length || line[digits] is not ('.' or ')'))
            return false;
        var next = digits + 1;
        if (next < line.Length && line[next] != ' ')
            return false;

        marker = new ListMarker(true, int.Parse(line.AsSpan(indent, digits - indent), System.Globalization.CultureInfo.InvariantCulture), line[digits], ContentOffset(line, next), indent);
        return true;
    }

    private static int ContentOffset(string line, int afterMarker)
    {
        var spaces = 0;
        while (afterMarker + spaces < line.Length && line[afterMarker + spaces] == ' ') spaces++;
        // 1–4 spaces after the marker belong to the marker; 5+ means indented code inside the item.
        return afterMarker + (spaces is >= 1 and <= 4 ? spaces : 1);
    }
}

namespace CL.WebLogic.Templates.Parsing;

/// <summary>Parses <c>name="value"</c> / <c>name=value</c> argument lists of partials and widgets.</summary>
internal static class TemplateAttributeSyntax
{
    /// <summary>Splits <c>"path rest…"</c> into the first whitespace-delimited token and the remainder.</summary>
    public static (string First, string Remainder) SplitFirstToken(string text)
    {
        var s = text.TrimStart();
        var end = 0;
        while (end < s.Length && !char.IsWhiteSpace(s[end])) end++;
        return (s.Substring(0, end).Trim(), end < s.Length ? s.Substring(end + 1) : string.Empty);
    }

    /// <summary>Yields every attribute; <c>Quoted</c> is true for quoted (literal) values.</summary>
    public static IEnumerable<(string Name, string Value, bool Quoted)> Parse(string text)
    {
        var i = 0;
        var n = text.Length;
        while (i < n)
        {
            while (i < n && char.IsWhiteSpace(text[i])) i++;
            if (i >= n) break;

            var nameStart = i;
            while (i < n && text[i] != '=' && !char.IsWhiteSpace(text[i])) i++;
            var name = text.Substring(nameStart, i - nameStart);
            if (name.Length == 0) { i++; continue; }

            while (i < n && char.IsWhiteSpace(text[i])) i++;
            if (i >= n || text[i] != '=') { yield return (name, string.Empty, true); continue; }
            i++;
            while (i < n && char.IsWhiteSpace(text[i])) i++;
            if (i >= n) { yield return (name, string.Empty, true); break; }

            if (text[i] == '"' || text[i] == '\'')
            {
                var quote = text[i++];
                var start = i;
                while (i < n && text[i] != quote) i++;
                var value = text.Substring(start, i - start);
                if (i < n) i++;
                yield return (name, value, true);
            }
            else
            {
                var start = i;
                while (i < n && !char.IsWhiteSpace(text[i])) i++;
                yield return (name, text.Substring(start, i - start), false);
            }
        }
    }
}

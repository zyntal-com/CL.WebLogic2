using System.Net;
using System.Text;
using CL.WebLogic.Content.Models;

namespace CL.WebLogic.Content.Services;

/// <summary>
/// A forgiving HTML tokenizer modelled on the browser algorithm closely enough for sanitizing:
/// raw-text elements (script, style, textarea…) swallow everything up to their end tag,
/// comments end at <c>--&gt;</c> (or the end of input), stray <c>&lt;</c> is text, and entity
/// references are decoded. It never throws on malformed input.
/// </summary>
public static class HtmlTokenizer
{
    private static readonly HashSet<string> RawTextElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "style", "textarea", "title", "xmp", "iframe", "noembed", "noframes", "noscript", "plaintext"
    };

    /// <summary>Tokenizes <paramref name="html"/>.</summary>
    public static IEnumerable<HtmlToken> Tokenize(string? html)
    {
        var s = html ?? string.Empty;
        var i = 0;
        var text = new StringBuilder();

        while (i < s.Length)
        {
            if (s[i] != '<' || i + 1 >= s.Length)
            {
                text.Append(s[i++]);
                continue;
            }

            var next = s[i + 1];
            HtmlToken? token = null;
            var end = i;

            if (next == '!' && string.CompareOrdinal(s, i, "<!--", 0, 4) == 0)
            {
                var close = s.IndexOf("-->", i + 4, StringComparison.Ordinal);
                end = close < 0 ? s.Length : close + 3;
                token = new HtmlToken(HtmlTokenKind.Comment, string.Empty, s.Substring(i + 4, (close < 0 ? s.Length : close) - i - 4), [], false);
            }
            else if (next is '!' or '?')
            {
                var close = s.IndexOf('>', i + 2);
                end = close < 0 ? s.Length : close + 1;
                var body = s.Substring(i + 2, (close < 0 ? s.Length : close) - i - 2);
                token = body.StartsWith("doctype", StringComparison.OrdinalIgnoreCase)
                    ? new HtmlToken(HtmlTokenKind.Doctype, string.Empty, body, [], false)
                    : new HtmlToken(HtmlTokenKind.Comment, string.Empty, body, [], false);
            }
            else if (char.IsAsciiLetter(next))
            {
                token = ReadTag(s, i + 1, isEnd: false, out end);
            }
            else if (next == '/' && i + 2 < s.Length && char.IsAsciiLetter(s[i + 2]))
            {
                token = ReadTag(s, i + 2, isEnd: true, out end);
            }
            else if (next == '/' && i + 2 < s.Length && s[i + 2] == '>')
            {
                end = i + 3; // "</>" is ignored by browsers
                i = end;
                continue;
            }

            if (token is null)
            {
                text.Append(s[i++]);
                continue;
            }

            if (text.Length > 0)
            {
                yield return HtmlToken.ForText(WebUtility.HtmlDecode(text.ToString()));
                text.Clear();
            }

            yield return token;
            i = end;

            if (token.Kind == HtmlTokenKind.StartTag && !token.SelfClosing && RawTextElements.Contains(token.Name))
            {
                var closeIndex = FindRawTextEnd(s, i, token.Name);
                if (closeIndex > i)
                    yield return HtmlToken.ForText(s.Substring(i, closeIndex - i));
                i = closeIndex;
            }
        }

        if (text.Length > 0)
            yield return HtmlToken.ForText(WebUtility.HtmlDecode(text.ToString()));
    }

    private static int FindRawTextEnd(string s, int from, string name)
    {
        var search = from;
        while (true)
        {
            var index = s.IndexOf("</", search, StringComparison.Ordinal);
            if (index < 0)
                return s.Length;
            var after = index + 2 + name.Length;
            if (after <= s.Length
                && string.Compare(s, index + 2, name, 0, name.Length, StringComparison.OrdinalIgnoreCase) == 0
                && (after == s.Length || char.IsWhiteSpace(s[after]) || s[after] is '>' or '/'))
                return index;
            search = index + 2;
        }
    }

    private static HtmlToken ReadTag(string s, int nameStart, bool isEnd, out int end)
    {
        var i = nameStart;
        while (i < s.Length && !char.IsWhiteSpace(s[i]) && s[i] != '/' && s[i] != '>') i++;
        var name = s.Substring(nameStart, i - nameStart).ToLowerInvariant();

        var attributes = new List<KeyValuePair<string, string>>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var selfClosing = false;

        while (i < s.Length && s[i] != '>')
        {
            var c = s[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }
            if (c == '/')
            {
                selfClosing = i + 1 < s.Length && s[i + 1] == '>';
                i++;
                continue;
            }

            var attrStart = i;
            i++; // the first character always belongs to the name (even '=' — browser behaviour)
            while (i < s.Length && !char.IsWhiteSpace(s[i]) && s[i] is not ('/' or '>' or '=')) i++;
            var attrName = s.Substring(attrStart, i - attrStart).ToLowerInvariant();

            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            var value = string.Empty;
            if (i < s.Length && s[i] == '=')
            {
                i++;
                while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
                if (i < s.Length && s[i] is '"' or '\'')
                {
                    var quote = s[i++];
                    var close = s.IndexOf(quote, i);
                    var valueEnd = close < 0 ? s.Length : close;
                    value = s.Substring(i, valueEnd - i);
                    i = close < 0 ? s.Length : close + 1;
                }
                else
                {
                    var valueStart = i;
                    while (i < s.Length && !char.IsWhiteSpace(s[i]) && s[i] != '>') i++;
                    value = s.Substring(valueStart, i - valueStart);
                }
            }

            if (!isEnd && seen.Add(attrName))
                attributes.Add(new KeyValuePair<string, string>(attrName, WebUtility.HtmlDecode(value)));
        }

        end = i < s.Length ? i + 1 : s.Length;
        return new HtmlToken(isEnd ? HtmlTokenKind.EndTag : HtmlTokenKind.StartTag, name, string.Empty, attributes, selfClosing && !isEnd);
    }
}

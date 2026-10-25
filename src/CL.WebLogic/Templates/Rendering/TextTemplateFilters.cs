using System.Text;

namespace CL.WebLogic.Templates.Rendering;

/// <summary>Text filters: upper, lower, trim, capitalize, truncate, replace, default, prefix, suffix, length, reverse, wordcount, slug, url.</summary>
internal static class TextTemplateFilters
{
    public static void Register(IDictionary<string, Func<object?, string?, TemplateRenderContext, object?>> filters)
    {
        filters["upper"] = (v, _, _) => Text(v).ToUpperInvariant();
        filters["uppercase"] = filters["upper"];
        filters["lower"] = (v, _, _) => Text(v).ToLowerInvariant();
        filters["lowercase"] = filters["lower"];
        filters["trim"] = (v, _, _) => Text(v).Trim();
        filters["capitalize"] = (v, _, _) => Capitalize(Text(v));
        filters["truncate"] = (v, a, _) => Truncate(Text(v), int.TryParse(a, out var n) && n > 0 ? n : 100);
        filters["replace"] = (v, a, _) => Replace(Text(v), a);
        filters["default"] = (v, a, _) => string.IsNullOrWhiteSpace(Text(v)) ? a : v;
        filters["prefix"] = (v, a, _) => string.IsNullOrEmpty(Text(v)) ? v : a + Text(v);
        filters["suffix"] = (v, a, _) => string.IsNullOrEmpty(Text(v)) ? v : Text(v) + a;
        filters["length"] = (v, _, _) => Length(v);
        filters["count"] = (v, _, _) => Length(v);
        filters["reverse"] = (v, _, _) => v is string s ? new string(s.Reverse().ToArray()) : v is System.Collections.IEnumerable e ? e.Cast<object?>().Reverse().ToList() : v;
        filters["wordcount"] = (v, _, _) => Text(v).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        filters["slug"] = (v, _, _) => Slugify(Text(v));
        filters["url"] = (v, _, _) => Uri.EscapeDataString(Text(v));
        filters["urlencode"] = filters["url"];
        filters["join"] = (v, a, _) => v is System.Collections.IEnumerable e and not string ? string.Join(a ?? ", ", e.Cast<object?>().Select(TemplateValues.ToText)) : v;
    }

    private static string Text(object? value) => TemplateValues.ToText(value);

    private static int Length(object? value) => value switch
    {
        null => 0,
        string s => s.Length,
        System.Collections.ICollection c => c.Count,
        System.Collections.IEnumerable e => e.Cast<object?>().Count(),
        _ => Text(value).Length
    };

    private static string Capitalize(string text) =>
        text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text.Substring(0, max).TrimEnd() + "...";

    private static string Replace(string text, string? argument)
    {
        if (string.IsNullOrEmpty(argument))
            return text;
        var parts = argument.Split("→", 2); // "a→b"
        if (parts.Length < 2)
            parts = argument.Split("->", 2);
        return parts.Length == 2 && parts[0].Length > 0 ? text.Replace(parts[0], parts[1], StringComparison.Ordinal) : text;
    }

    /// <summary>Lower-case ASCII slug: letters and digits, words joined by single hyphens.</summary>
    internal static string Slugify(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        var pendingHyphen = false;
        foreach (var c in normalized)
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                if (pendingHyphen && builder.Length > 0) builder.Append('-');
                builder.Append(char.ToLowerInvariant(c));
                pendingHyphen = false;
            }
            else if (char.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                pendingHyphen = true;
            }
        }
        return builder.ToString();
    }
}

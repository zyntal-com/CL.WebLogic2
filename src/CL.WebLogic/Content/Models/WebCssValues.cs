using System.Text.RegularExpressions;

namespace CL.WebLogic.Content.Models;

/// <summary>
/// Value checks for <see cref="WebHtmlSanitizerOptions.AllowedStyleProperties"/>. The sanitizer has
/// already refused values with characters outside letters, digits, space and <c># % . , ( ) - /</c>.
/// </summary>
public static partial class WebCssValues
{
    /// <summary>A percentage from 0 to 100 (<c>50%</c>, <c>33.5%</c>).</summary>
    public static bool Percentage(string value) =>
        PercentPattern().IsMatch(value) && double.TryParse(value.TrimEnd('%'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var n) && n is >= 0 and <= 100;

    /// <summary>A non-negative length: <c>12px</c>, <c>1.5em</c>, <c>2rem</c>, <c>50vw</c>, <c>0</c>, or a percentage up to 100.</summary>
    public static bool Length(string value) => value == "0" || LengthPattern().IsMatch(value) || Percentage(value);

    /// <summary>A colour: <c>#rgb</c>, <c>#rrggbb</c> (with alpha), <c>rgb()</c>/<c>rgba()</c>/<c>hsl()</c>/<c>hsla()</c> with numbers, or a colour keyword.</summary>
    public static bool Color(string value) => HexColor().IsMatch(value) || ColorFunction().IsMatch(value) || NamedColor().IsMatch(value);

    /// <summary>One of <paramref name="keywords"/> (case-insensitive).</summary>
    public static Func<string, bool> OneOf(params string[] keywords)
    {
        var set = new HashSet<string>(keywords, StringComparer.OrdinalIgnoreCase);
        return set.Contains;
    }

    /// <summary>Any of <paramref name="checks"/>.</summary>
    public static Func<string, bool> Any(params Func<string, bool>[] checks) => value => checks.Any(check => check(value));

    [GeneratedRegex(@"^\d{1,3}(\.\d{1,3})?%$")]
    private static partial Regex PercentPattern();

    [GeneratedRegex(@"^\d{1,4}(\.\d{1,3})?(px|em|rem|vw|vh|ch|pt)$", RegexOptions.IgnoreCase)]
    private static partial Regex LengthPattern();

    [GeneratedRegex(@"^#([0-9a-f]{3,4}|[0-9a-f]{6}|[0-9a-f]{8})$", RegexOptions.IgnoreCase)]
    private static partial Regex HexColor();

    [GeneratedRegex(@"^(rgb|rgba|hsl|hsla)\(\s*\d{1,3}(\.\d+)?%?(\s*[, ]\s*\d{1,3}(\.\d+)?%?){2}(\s*[,/]\s*(0|1|0?\.\d+|\d{1,3}%))?\s*\)$", RegexOptions.IgnoreCase)]
    private static partial Regex ColorFunction();

    [GeneratedRegex(@"^[a-z]{3,20}$", RegexOptions.IgnoreCase)]
    private static partial Regex NamedColor();
}

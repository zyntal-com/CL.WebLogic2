using System.Globalization;

namespace CL.WebLogic.Templates.Rendering;

/// <summary>
/// Number, date and size filters: number, percent, round, abbreviate, filesize, format, date, timeago.
/// Invariant culture, so output is deterministic across hosts (localize with a custom filter).
/// </summary>
internal static class NumberTemplateFilters
{
    public static void Register(IDictionary<string, Func<object?, string?, TemplateRenderContext, object?>> filters)
    {
        filters["number"] = (v, a, _) => TemplateValues.ToNumber(v) is { } n ? n.ToString("N" + Decimals(a), CultureInfo.InvariantCulture) : v;
        filters["percent"] = (v, a, _) => TemplateValues.ToNumber(v) is { } n ? n.ToString("F" + Decimals(a), CultureInfo.InvariantCulture) + "%" : v;
        filters["round"] = (v, a, _) => TemplateValues.ToNumber(v) is { } n ? Math.Round(n, Decimals(a), MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture) : v;
        filters["abbreviate"] = (v, _, _) => TemplateValues.ToNumber(v) is { } n ? Abbreviate(n) : v;
        filters["filesize"] = (v, _, _) => TemplateValues.ToNumber(v) is { } n ? FileSize(n) : v;
        filters["format"] = (v, a, _) => v is IFormattable f && !string.IsNullOrWhiteSpace(a) ? f.ToString(a, CultureInfo.InvariantCulture) : v;
        filters["date"] = filters["format"];
        filters["timeago"] = (v, _, _) => TimeAgo(v);
    }

    private static int Decimals(string? argument) => int.TryParse(argument, out var d) ? Math.Clamp(d, 0, 15) : 0;

    private static string Abbreviate(double number)
    {
        var abs = Math.Abs(number);
        var sign = number < 0 ? "-" : string.Empty;
        return abs switch
        {
            >= 1_000_000_000 => sign + One(abs / 1_000_000_000) + "B",
            >= 1_000_000 => sign + One(abs / 1_000_000) + "M",
            >= 1_000 => sign + One(abs / 1_000) + "k",
            _ => number.ToString("0.##", CultureInfo.InvariantCulture)
        };

        static string One(double v) => (Math.Floor(v * 10) / 10).ToString("0.#", CultureInfo.InvariantCulture);
    }

    private static string FileSize(double bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var size = Math.Max(0, bytes);
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }
        return size.ToString(unit == 0 ? "0" : "0.#", CultureInfo.InvariantCulture) + " " + units[unit];
    }

    private static object? TimeAgo(object? value)
    {
        DateTime? utc = value switch
        {
            DateTime dt => dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime(),
            DateTimeOffset dto => dto.UtcDateTime,
            _ => null
        };
        if (utc is null)
            return value;

        var span = DateTime.UtcNow - utc.Value;
        if (span < TimeSpan.Zero) span = TimeSpan.Zero;
        return span.TotalSeconds switch
        {
            < 60 => "just now",
            < 3600 => $"{(int)span.TotalMinutes}m ago",
            < 86400 => $"{(int)span.TotalHours}h ago",
            < 86400 * 30 => $"{(int)span.TotalDays}d ago",
            _ => utc.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        };
    }
}

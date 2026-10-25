using System.Collections;
using System.Globalization;
using CL.WebLogic.Templates.Ast;
using CL.WebLogic.Templates.Escaping;

namespace CL.WebLogic.Templates.Rendering;

/// <summary>Value semantics shared by the interpreter, filters and compiled templates.</summary>
public static class TemplateValues
{
    /// <summary>Text form of a value: lists of strings are joined, formattables use the current culture.</summary>
    public static string ToText(object? value) => value switch
    {
        null => string.Empty,
        string s => s,
        bool b => b ? "true" : "false",
        WebHtml html => html.Html,
        EncodedValue encoded => encoded.Text,
        IEnumerable<string> strings => string.Join(", ", strings),
        IFormattable formattable => formattable.ToString(null, CultureInfo.CurrentCulture),
        _ => value.ToString() ?? string.Empty
    };

    /// <summary>Truthiness for <c>{if}</c>: null, false, empty / whitespace, "false", "0", zero and empty collections are false.</summary>
    public static bool Truthy(object? value) => value switch
    {
        null => false,
        bool b => b,
        string s => !string.IsNullOrWhiteSpace(s) && !string.Equals(s, "false", StringComparison.OrdinalIgnoreCase) && s != "0",
        int i => i != 0,
        long l => l != 0,
        double d => d != 0,
        decimal m => m != 0,
        WebHtml html => html.Html.Length > 0,
        IEnumerable enumerable => enumerable.Cast<object?>().Any(),
        _ => true
    };

    /// <summary>Items of a <c>{foreach}</c> source; strings and non-enumerables yield none.</summary>
    public static IReadOnlyList<object?> AsItems(object? source) =>
        source is string || source is not IEnumerable enumerable ? [] : enumerable.Cast<object?>().ToList();

    /// <summary>Numeric-aware comparison, falling back to case-insensitive text comparison.</summary>
    public static bool Compare(object? left, CompareOp op, object? right)
    {
        var l = ToNumber(left);
        var r = ToNumber(right);
        var result = l.HasValue && r.HasValue
            ? l.Value.CompareTo(r.Value)
            : string.Compare(ToText(left), ToText(right), StringComparison.OrdinalIgnoreCase);

        return op switch
        {
            CompareOp.Eq => result == 0,
            CompareOp.Ne => result != 0,
            CompareOp.Gt => result > 0,
            CompareOp.Gte => result >= 0,
            CompareOp.Lt => result < 0,
            CompareOp.Lte => result <= 0,
            _ => false
        };
    }

    /// <summary>A number from numeric types or invariant-culture strings, else null.</summary>
    public static double? ToNumber(object? value) => value switch
    {
        null => null,
        int i => i,
        long l => l,
        double d => d,
        float f => f,
        decimal m => (double)m,
        short s => s,
        byte b => b,
        string s when double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) => parsed,
        _ => null
    };
}

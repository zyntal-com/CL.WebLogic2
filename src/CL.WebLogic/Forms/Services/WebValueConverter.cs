using System.Globalization;

namespace CL.WebLogic.Forms.Services;

/// <summary>Converts posted strings to property values (invariant culture, as browsers post them).</summary>
public static class WebValueConverter
{
    private static readonly HashSet<Type> Numeric =
    [
        typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint),
        typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal)
    ];

    /// <summary>True for numeric types.</summary>
    public static bool IsNumeric(Type type) => Numeric.Contains(type);

    /// <summary>The element type of supported collections (arrays, List, IEnumerable, IReadOnlyList, HashSet…), else null.</summary>
    public static Type? CollectionElement(Type type)
    {
        if (type == typeof(string)) return null;
        if (type.IsArray) return type.GetElementType();
        if (!type.IsGenericType) return null;
        var definition = type.GetGenericTypeDefinition();
        return definition == typeof(List<>) || definition == typeof(IList<>) || definition == typeof(IReadOnlyList<>)
            || definition == typeof(IEnumerable<>) || definition == typeof(ICollection<>) || definition == typeof(IReadOnlyCollection<>)
            || definition == typeof(HashSet<>) || definition == typeof(ISet<>)
            ? type.GetGenericArguments()[0]
            : null;
    }

    /// <summary>Builds a collection of <paramref name="collectionType"/> from <paramref name="items"/>.</summary>
    public static object CreateCollection(Type collectionType, Type element, IEnumerable<object?> items)
    {
        var list = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(element))!;
        foreach (var item in items)
            list.Add(item);
        if (collectionType.IsArray)
        {
            var array = Array.CreateInstance(element, list.Count);
            list.CopyTo(array, 0);
            return array;
        }
        if (collectionType.IsGenericType && collectionType.GetGenericTypeDefinition() is var d && (d == typeof(HashSet<>) || d == typeof(ISet<>)))
            return Activator.CreateInstance(typeof(HashSet<>).MakeGenericType(element), list)!;
        return list;
    }

    /// <summary>Converts <paramref name="text"/> (already trimmed and non-empty) to <paramref name="type"/>.</summary>
    public static bool TryConvert(string text, Type type, out object? value)
    {
        value = null;
        var c = CultureInfo.InvariantCulture;
        // Enums report their underlying type code, so they are handled first; only names are accepted.
        if (type.IsEnum)
        {
            if (char.IsAsciiDigit(text[0]) || text[0] == '-' || !Enum.TryParse(type, text, ignoreCase: true, out var e) || e is null || !Enum.IsDefined(type, e))
                return false;
            value = e;
            return true;
        }
        switch (Type.GetTypeCode(type))
        {
            case TypeCode.String: value = text; return true;
            case TypeCode.Boolean:
                value = text is "true" or "True" or "on" or "1" or "yes";
                return value is true || text is "false" or "False" or "off" or "0" or "no";
            case TypeCode.Int32: return Set(int.TryParse(text, NumberStyles.Integer, c, out var i), i, out value);
            case TypeCode.Int64: return Set(long.TryParse(text, NumberStyles.Integer, c, out var l), l, out value);
            case TypeCode.Int16: return Set(short.TryParse(text, NumberStyles.Integer, c, out var s), s, out value);
            case TypeCode.Byte: return Set(byte.TryParse(text, NumberStyles.Integer, c, out var b), b, out value);
            case TypeCode.SByte: return Set(sbyte.TryParse(text, NumberStyles.Integer, c, out var sb), sb, out value);
            case TypeCode.UInt16: return Set(ushort.TryParse(text, NumberStyles.Integer, c, out var us), us, out value);
            case TypeCode.UInt32: return Set(uint.TryParse(text, NumberStyles.Integer, c, out var ui), ui, out value);
            case TypeCode.UInt64: return Set(ulong.TryParse(text, NumberStyles.Integer, c, out var ul), ul, out value);
            case TypeCode.Decimal: return Set(decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, c, out var m), m, out value);
            case TypeCode.Double: return Set(double.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, c, out var d) && double.IsFinite(d), d, out value);
            case TypeCode.Single: return Set(float.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, c, out var f) && float.IsFinite(f), f, out value);
            case TypeCode.DateTime: return Set(DateTime.TryParse(text, c, DateTimeStyles.AssumeLocal, out var dt), dt, out value);
        }
        if (type == typeof(DateOnly)) return Set(DateOnly.TryParseExact(text, "yyyy-MM-dd", c, DateTimeStyles.None, out var date), date, out value);
        if (type == typeof(TimeOnly)) return Set(TimeOnly.TryParse(text, c, DateTimeStyles.None, out var time), time, out value);
        if (type == typeof(TimeSpan)) return Set(TimeSpan.TryParse(text, c, out var span), span, out value);
        if (type == typeof(DateTimeOffset)) return Set(DateTimeOffset.TryParse(text, c, DateTimeStyles.AssumeLocal, out var dto), dto, out value);
        if (type == typeof(Guid)) return Set(Guid.TryParse(text, out var g), g, out value);
        return false;
    }

    /// <summary>Formats a value the way the matching input type expects.</summary>
    public static string Format(object? value) => value switch
    {
        null => "",
        bool b => b ? "true" : "false",
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        TimeOnly t => t.ToString("HH:mm", CultureInfo.InvariantCulture),
        DateTime dt => dt.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture),
        DateTimeOffset dto => dto.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture),
        Enum e => e.ToString(),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? ""
    };

    private static bool Set<T>(bool ok, T parsed, out object? value)
    {
        value = ok ? parsed : null;
        return ok;
    }
}

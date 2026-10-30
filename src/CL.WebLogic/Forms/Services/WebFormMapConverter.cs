using System.Collections;
using System.Globalization;

namespace CL.WebLogic.Forms.Services;

/// <summary>
/// The value conversions <see cref="WebFormMapper"/> allows: assignable values, nullable wrappers,
/// enums by name, numbers without overflow or lost fractions, strings to and from numbers, enums,
/// <see cref="Guid"/>, dates and times (invariant culture), <see cref="DateTime"/> ↔ <see cref="DateOnly"/>
/// and collections of those. Anything else is not converted.
/// </summary>
internal static class WebFormMapConverter
{
    /// <summary>Converts <paramref name="value"/> (not null) to <paramref name="targetType"/>; false when it cannot be done safely.</summary>
    public static bool TryConvert(object value, Type targetType, out object? result)
    {
        result = null;
        var underlying = Nullable.GetUnderlyingType(targetType);
        var type = underlying ?? targetType;
        var acceptsNull = !targetType.IsValueType || underlying is not null;

        if (WebValueConverter.CollectionElement(type) is { } element)
            return TryConvertCollection(value, type, element, out result);

        if (type.IsInstanceOfType(value))
        {
            result = value;
            return true;
        }

        if (value is string text)
        {
            text = text.Trim();
            if (text.Length == 0)
                return acceptsNull; // an empty field clears a nullable value
            return WebValueConverter.TryConvert(text, type, out result);
        }

        if (type == typeof(string))
        {
            result = Format(value);
            return true;
        }

        if (type.IsEnum)
            return TryConvertToEnum(value, type, out result);

        if (value is Enum && WebValueConverter.IsNumeric(type))
            value = Convert.ChangeType(value, Enum.GetUnderlyingType(value.GetType()), CultureInfo.InvariantCulture);

        if (WebValueConverter.IsNumeric(value.GetType()) && WebValueConverter.IsNumeric(type))
            return TryConvertNumber(value, type, out result);

        switch (value)
        {
            case DateTime dateTime when type == typeof(DateOnly):
                result = DateOnly.FromDateTime(dateTime);
                return true;
            case DateOnly date when type == typeof(DateTime):
                result = date.ToDateTime(TimeOnly.MinValue);
                return true;
            default:
                return false;
        }
    }

    /// <summary>Formats a value as text (invariant culture; dates in ISO 8601).</summary>
    public static string Format(object value) => value switch
    {
        bool b => b ? "true" : "false",
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        TimeOnly t => t.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
        DateTime dt => dt.ToString("O", CultureInfo.InvariantCulture),
        DateTimeOffset dto => dto.ToString("O", CultureInfo.InvariantCulture),
        Guid g => g.ToString("D"),
        Enum e => e.ToString(),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? ""
    };

    private static bool TryConvertCollection(object value, Type type, Type element, out object? result)
    {
        result = null;
        if (value is string || value is not IEnumerable items)
            return false;
        var elementAcceptsNull = !element.IsValueType || Nullable.GetUnderlyingType(element) is not null;
        var converted = new List<object?>();
        foreach (var item in items)
        {
            if (item is null)
            {
                if (!elementAcceptsNull)
                    return false;
                converted.Add(null);
            }
            else if (TryConvert(item, element, out var one) && (one is not null || elementAcceptsNull))
            {
                converted.Add(one);
            }
            else
            {
                return false;
            }
        }
        // Always a new collection: the form and the entity never share a mutable list.
        result = WebValueConverter.CreateCollection(type, element, converted);
        return true;
    }

    private static bool TryConvertToEnum(object value, Type type, out object? result)
    {
        result = null;
        if (value is Enum other)
        {
            if (!Enum.TryParse(type, other.ToString(), ignoreCase: false, out var parsed) || parsed is null || !Enum.IsDefined(type, parsed))
                return false;
            result = parsed;
            return true;
        }
        if (value is byte or sbyte or short or ushort or int or uint or long or ulong)
        {
            var candidate = Enum.ToObject(type, value);
            if (!Enum.IsDefined(type, candidate))
                return false;
            result = candidate;
            return true;
        }
        return false;
    }

    private static bool TryConvertNumber(object value, Type type, out object? result)
    {
        result = null;
        try
        {
            if (IsIntegral(type) && value is float or double or decimal)
            {
                var exact = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                if (decimal.Truncate(exact) != exact)
                    return false; // never drop a fraction silently
            }
            result = Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
            return result switch
            {
                float f => float.IsFinite(f),
                double d => double.IsFinite(d),
                _ => true
            };
        }
        catch (OverflowException)
        {
            result = null;
            return false;
        }
    }

    private static bool IsIntegral(Type type) =>
        type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) || type == typeof(ushort)
        || type == typeof(int) || type == typeof(uint) || type == typeof(long) || type == typeof(ulong);
}

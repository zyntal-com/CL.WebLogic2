using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace CL.WebLogic.Templates.Rendering;

/// <summary>
/// Walks dotted paths (<c>user.address.city</c>) through dictionaries and public properties.
/// Property lookups are cached per type in a <see cref="ConditionalWeakTable{TKey,TValue}"/> so
/// plugin model types can still be unloaded.
/// </summary>
public static class TemplatePathWalker
{
    private static readonly ConditionalWeakTable<Type, Dictionary<string, PropertyInfo>> Properties = new();

    /// <summary>Resolves <paramref name="path"/> against <paramref name="source"/>; null when any segment is missing.</summary>
    public static object? Resolve(object? source, string path)
    {
        if (source is null || string.IsNullOrWhiteSpace(path))
            return string.IsNullOrWhiteSpace(path) ? source : null;

        var current = source;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (current is null)
                return null;
            current = Step(current, segment);
        }
        return current;
    }

    private static object? Step(object current, string segment)
    {
        switch (current)
        {
            case IReadOnlyDictionary<string, object?> ro:
                return ro.TryGetValue(segment, out var a) ? a : FindIgnoreCase(ro, segment);
            case IDictionary<string, object?> rw:
                return rw.TryGetValue(segment, out var b) ? b : null;
            case IReadOnlyDictionary<string, string> sro:
                return sro.TryGetValue(segment, out var c) ? c : null;
            case IDictionary dictionary:
                return dictionary.Contains(segment) ? dictionary[segment] : null;
            case IList list when int.TryParse(segment, out var index):
                return index >= 0 && index < list.Count ? list[index] : null;
        }

        var map = Properties.GetValue(current.GetType(), static type => type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0)
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase));

        return map.TryGetValue(segment, out var property) ? property.GetValue(current) : null;
    }

    private static object? FindIgnoreCase(IReadOnlyDictionary<string, object?> dictionary, string key)
    {
        foreach (var (k, v) in dictionary)
        {
            if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
                return v;
        }
        return null;
    }
}

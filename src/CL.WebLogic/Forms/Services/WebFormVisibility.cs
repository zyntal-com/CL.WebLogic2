using CL.WebLogic.Forms.Models;
using Microsoft.Extensions.Primitives;

namespace CL.WebLogic.Forms.Services;

/// <summary>Evaluates show-when conditions against posted values (a field controlled by a hidden field is hidden too).</summary>
public static class WebFormVisibility
{
    /// <summary>A predicate: is the field with this (possibly prefixed) key visible?</summary>
    public static Func<string, bool> For(WebFormDefinition definition, IReadOnlyDictionary<string, StringValues> values)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return key => IsVisible(definition, values, key, 0);
    }

    private static bool IsVisible(WebFormDefinition definition, IReadOnlyDictionary<string, StringValues> values, string key, int depth)
    {
        if (depth > 10)
            return false;
        var bracket = key.LastIndexOf("].", StringComparison.Ordinal);
        var scope = bracket < 0 ? "" : key[..(bracket + 2)];
        var name = bracket < 0 ? key : key[(bracket + 2)..];
        var form = bracket < 0 ? definition : Nested(definition, key[..key.IndexOf('[', StringComparison.Ordinal)]) ?? definition;

        var field = form.Field(name);
        if (field?.ShowWhen is not { } condition)
            return true;
        var controllerKey = scope + condition.Field;
        if (!IsVisible(definition, values, controllerKey, depth + 1))
            return false;

        values.TryGetValue(controllerKey, out var posted);
        var controller = form.Field(condition.Field);
        var current = controller?.Type == WebFieldType.Checkbox
            ? (posted.Count > 0 && posted[^1] is "true" or "on" or "1" ? "true" : "false")
            : posted.Count > 0 ? posted[0] ?? "" : "";
        return posted.Count > 1 && controller?.Type != WebFieldType.Checkbox
            ? posted.Any(p => condition.Values.Contains(p ?? "", StringComparer.OrdinalIgnoreCase))
            : condition.Values.Contains(current, StringComparer.OrdinalIgnoreCase);
    }

    private static WebFormDefinition? Nested(WebFormDefinition definition, string path)
    {
        // Only one repeater level is resolved by name; deeper keys fall back to the root definition.
        var root = path.Contains('.', StringComparison.Ordinal) ? path[(path.LastIndexOf('.') + 1)..] : path;
        return definition.Field(root)?.Repeater;
    }
}

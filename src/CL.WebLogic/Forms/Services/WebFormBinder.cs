using System.Text.RegularExpressions;
using CL.WebLogic.Content.Services;
using CL.WebLogic.Forms.Models;
using CL.WebLogic.Forms.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace CL.WebLogic.Forms.Services;

/// <summary>
/// Binds posted values to a new model instance. Only defined fields bind (no over-posting);
/// read-only and hidden (show-when) fields keep their defaults; conversion failures and invalid
/// fixed choices become errors. Files are bound separately by the upload checks. Posted names match field
/// names case-insensitively when the values dictionary uses a case-insensitive comparer (the form service does).
/// </summary>
public sealed partial class WebFormBinder
{
    private readonly WebFormMessages _messages;
    private readonly IWebHtmlSanitizer _sanitizer;

    /// <summary>Creates the binder.</summary>
    public WebFormBinder(WebFormMessages messages, IWebHtmlSanitizer sanitizer)
    {
        _messages = messages ?? throw new ArgumentNullException(nameof(messages));
        _sanitizer = sanitizer ?? throw new ArgumentNullException(nameof(sanitizer));
    }

    /// <summary>Binds <paramref name="fields"/> of <paramref name="definition"/> from <paramref name="values"/>.</summary>
    public object Bind(WebFormDefinition definition, IEnumerable<WebFieldDefinition> fields, IReadOnlyDictionary<string, StringValues> values,
        Func<string, bool> isVisible, WebFormErrors errors, string prefix = "")
    {
        var model = Activator.CreateInstance(definition.ModelType)
            ?? throw new InvalidOperationException($"Form model '{definition.ModelType}' needs a public parameterless constructor.");
        foreach (var field in fields)
        {
            var key = prefix + field.Name;
            if (field.ReadOnly || field.Type == WebFieldType.File || !isVisible(key))
                continue;

            if (field.Repeater is not null)
            {
                BindRepeater(field, key, values, isVisible, errors, model);
                continue;
            }

            values.TryGetValue(key, out var posted);
            if (field.Type == WebFieldType.Checkbox && field.ValueType == typeof(bool))
            {
                field.Property.SetValue(model, posted.Count > 0 && WebValueConverter.TryConvert(posted[^1] ?? "", typeof(bool), out var flag) && flag is true);
                continue;
            }

            if (field.ElementType is not null)
            {
                BindMany(field, key, posted, errors, model);
                continue;
            }

            var raw = posted.Count > 0 ? posted[0] ?? "" : "";
            var text = field.Type is WebFieldType.Password or WebFieldType.TextArea or WebFieldType.RichText ? raw : raw.Trim();
            if (text.Length == 0)
                continue;
            if (field.Type == WebFieldType.RichText)
                text = _sanitizer.Sanitize(text).Html;

            if (!WebValueConverter.TryConvert(text, field.ValueType, out var converted))
            {
                errors.Add(key, _messages.Format(WebValueConverter.IsNumeric(field.ValueType) ? "number" : "invalid", field.Label));
                continue;
            }
            if (!IsAllowedChoice(field, text))
            {
                errors.Add(key, _messages.Format("choice", field.Label));
                continue;
            }
            field.Property.SetValue(model, converted);
        }
        return model;
    }

    private void BindMany(WebFieldDefinition field, string key, StringValues posted, WebFormErrors errors, object model)
    {
        var items = new List<object?>();
        foreach (var raw in posted)
        {
            var text = (raw ?? "").Trim();
            if (text.Length == 0)
                continue;
            if (!WebValueConverter.TryConvert(text, field.ElementType!, out var converted) || !IsAllowedChoice(field, text))
            {
                errors.Add(key, _messages.Format("choice", field.Label));
                return;
            }
            if (!items.Contains(converted))
                items.Add(converted);
        }
        field.Property.SetValue(model, WebValueConverter.CreateCollection(field.Property.PropertyType, field.ElementType!, items));
    }

    private void BindRepeater(WebFieldDefinition field, string key, IReadOnlyDictionary<string, StringValues> values, Func<string, bool> isVisible, WebFormErrors errors, object model)
    {
        var indexes = new SortedSet<int>();
        var prefix = key + "[";
        foreach (var name in values.Keys)
        {
            if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;
            var match = RepeaterIndex().Match(name, prefix.Length);
            if (match.Success && match.Index == prefix.Length && int.TryParse(match.Groups[1].Value, out var index) && index < 10_000)
                indexes.Add(index);
        }

        var items = new List<object?>();
        foreach (var index in indexes.Take(field.RepeaterMax + 1))
            items.Add(Bind(field.Repeater!, field.Repeater!.Fields, values, isVisible, errors, $"{key}[{index}]."));
        // Re-number the errors of renumbered items is not needed: items keep their posted index order.
        field.Property.SetValue(model, WebValueConverter.CreateCollection(field.Property.PropertyType, field.ElementType!, items));
    }

    private static bool IsAllowedChoice(WebFieldDefinition field, string value) =>
        field.Options.Count == 0
        || field.Type is WebFieldType.Text or WebFieldType.Autocomplete
        || field.Options.Any(o => !o.Disabled && string.Equals(o.Value, value, StringComparison.OrdinalIgnoreCase));

    [GeneratedRegex(@"(\d{1,4})\]\.", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex RepeaterIndex();
}

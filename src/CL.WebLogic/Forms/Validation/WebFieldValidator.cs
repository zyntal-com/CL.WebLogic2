using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.RegularExpressions;
using CL.WebLogic.Forms.Models;
using CL.WebLogic.Forms.Services;
using CL.WebLogic.Forms.Uploads;

namespace CL.WebLogic.Forms.Validation;

/// <summary>
/// Applies the field rules to a bound model: required, lengths (in characters, like the
/// browser), ranges, patterns (1 s timeout), e-mail/URL/phone formats, equality and any other
/// DataAnnotations attribute, then <see cref="IValidatableObject"/>. Repeaters recurse.
/// </summary>
public sealed partial class WebFieldValidator
{
    private static readonly TimeSpan PatternTimeout = TimeSpan.FromSeconds(1);
    private readonly WebFormMessages _messages;

    /// <summary>Creates the validator.</summary>
    public WebFieldValidator(WebFormMessages messages) => _messages = messages ?? throw new ArgumentNullException(nameof(messages));

    /// <summary>Validates the visible <paramref name="fields"/> of <paramref name="model"/>.</summary>
    public void Validate(WebFormDefinition definition, IEnumerable<WebFieldDefinition> fields, object model, Func<string, bool> isVisible, WebFormErrors errors, string prefix = "")
    {
        ArgumentNullException.ThrowIfNull(model);
        foreach (var field in fields)
        {
            var key = prefix + field.Name;
            if (!isVisible(key) || errors.Has(key))
                continue;
            var value = field.Property.GetValue(model);
            ValidateValue(definition, field, model, value, key, errors);

            if (field.Repeater is not null && value is System.Collections.IEnumerable items)
            {
                var index = 0;
                foreach (var item in items)
                {
                    if (item is not null)
                        Validate(field.Repeater, field.Repeater.Fields, item, isVisible, errors, $"{key}[{index}].");
                    index++;
                }
            }
        }

        if (model is IValidatableObject validatable && !errors.Any)
        {
            foreach (var result in validatable.Validate(new ValidationContext(model)))
            {
                var members = result.MemberNames.ToList();
                if (members.Count == 0) errors.Add(prefix.TrimEnd('.'), result.ErrorMessage ?? _messages.Format("invalid", definition.Title ?? definition.Id));
                foreach (var member in members) errors.Add(prefix + member, result.ErrorMessage ?? _messages.Format("invalid", member));
            }
        }
    }

    private void ValidateValue(WebFormDefinition definition, WebFieldDefinition field, object model, object? value, string key, WebFormErrors errors)
    {
        if (field.Repeater is not null)
        {
            var count = value is System.Collections.ICollection items ? items.Count : 0;
            if (count < field.RepeaterMin) errors.Add(key, _messages.Format("repeaterMin", field.Label, field.RepeaterMin));
            if (count > field.RepeaterMax) errors.Add(key, _messages.Format("repeaterMax", field.Label, field.RepeaterMax));
        }

        if (IsEmpty(field, value))
        {
            if (field.Required)
                errors.Add(key, _messages.Format("required", field.Label));
            return;
        }

        if (value is string text)
        {
            var length = text.EnumerateRunes().Count();
            if (field.MinLength is { } min && length < min) errors.Add(key, _messages.Format("minLength", field.Label, min));
            if (field.MaxLength is { } max && length > max) errors.Add(key, _messages.Format("maxLength", field.Label, max));
            if (field.Type == WebFieldType.Email && !Email().IsMatch(text)) errors.Add(key, _messages.Format("email", field.Label));
            if (field.Type == WebFieldType.Url && !IsHttpUrl(text)) errors.Add(key, _messages.Format("url", field.Label));
            if (field.Type == WebFieldType.Tel && !Phone().IsMatch(text)) errors.Add(key, _messages.Format("tel", field.Label));
            if (field.Pattern is not null)
            {
                try
                {
                    if (!Regex.IsMatch(text, "^(?:" + field.Pattern + ")$", RegexOptions.CultureInvariant, PatternTimeout))
                        errors.Add(key, field.PatternMessage ?? _messages.Format("pattern", field.Label));
                }
                catch (RegexMatchTimeoutException)
                {
                    errors.Add(key, _messages.Format("timeout", field.Label));
                }
            }
        }

        if (value is IComparable comparable && value is not string)
        {
            if (field.Min is not null && WebValueConverter.TryConvert(field.Min, field.ValueType, out var min) && min is not null && comparable.CompareTo(min) < 0)
                errors.Add(key, _messages.Format("min", field.Label, field.Min));
            if (field.Max is not null && WebValueConverter.TryConvert(field.Max, field.ValueType, out var max) && max is not null && comparable.CompareTo(max) > 0)
                errors.Add(key, _messages.Format("max", field.Label, field.Max));
        }

        if (field.EqualTo is not null && definition.Field(field.EqualTo) is { } other && !Equals(other.Property.GetValue(model), value))
            errors.Add(key, _messages.Format("equalTo", field.Label, other.Label));

        foreach (var attribute in field.Validators)
        {
            var result = attribute.GetValidationResult(value, new ValidationContext(model) { MemberName = field.Name, DisplayName = field.Label });
            if (result != ValidationResult.Success)
                errors.Add(key, result?.ErrorMessage ?? _messages.Format("invalid", field.Label));
        }
    }

    /// <summary>Empty for the "required" rule: null, blank text, empty lists, an unchecked checkbox, no file.</summary>
    public static bool IsEmpty(WebFieldDefinition field, object? value) => value switch
    {
        null => true,
        string s => string.IsNullOrWhiteSpace(s),
        bool b => field.Type == WebFieldType.Checkbox && !b,
        WebUploadedFile => false,
        System.Collections.ICollection c => c.Count == 0,
        _ => false
    };

    private static bool IsHttpUrl(string text) =>
        Uri.TryCreate(text, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    [GeneratedRegex(@"^[^\s@<>"",;]+@[^\s@<>"",;.]+(\.[^\s@<>"",;.]+)+$", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex Email();

    [GeneratedRegex(@"^\+?[0-9 ()\-.]{3,32}$", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex Phone();
}

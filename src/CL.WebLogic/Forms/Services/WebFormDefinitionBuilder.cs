using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Text;
using CL.WebLogic.Forms.Attributes;
using CL.WebLogic.Forms.Models;
using CL.WebLogic.Forms.Uploads;

namespace CL.WebLogic.Forms.Services;

/// <summary>Builds a <see cref="WebFormDefinition"/> from a model type's properties and attributes.</summary>
public static class WebFormDefinitionBuilder
{
    private const int MaxDepth = 3;

    /// <summary>Builds the definition of <paramref name="type"/>.</summary>
    public static WebFormDefinition Build(Type type) => Build(type, 0);

    private static WebFormDefinition Build(Type type, int depth)
    {
        ArgumentNullException.ThrowIfNull(type);
        var form = type.GetCustomAttribute<WebFormAttribute>();
        var definition = new WebFormDefinition
        {
            Id = form?.Id ?? DefaultId(type),
            ModelType = type,
            Title = form?.Title,
            SubmitLabel = form?.SubmitLabel ?? "Submit",
            AntiBot = form?.AntiBot ?? true
        };

        var fields = new List<WebFieldDefinition>();
        var steps = new List<(WebFormStep Step, int FirstUse)>();
        var index = 0;
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance).OrderBy(p => p.MetadataToken))
        {
            index++;
            if (property.GetIndexParameters().Length > 0 || property.SetMethod is not { IsPublic: true } || property.GetMethod is not { IsPublic: true }
                || property.IsDefined(typeof(WebIgnoreAttribute), true))
                continue;
            var field = BuildField(property, index, depth);
            if (field is null)
                continue;
            fields.Add(field);

            if (property.GetCustomAttribute<WebStepAttribute>() is { } step)
            {
                field.StepName = step.Name;
                if (!steps.Any(s => string.Equals(s.Step.Name, step.Name, StringComparison.OrdinalIgnoreCase)))
                    steps.Add((new WebFormStep(step.Name, step.Title ?? Humanize(step.Name), step.Order), index));
            }
        }

        definition.Fields = [.. fields.OrderBy(f => f.Order)];
        definition.Steps = [.. steps.OrderBy(s => s.Step.Order).ThenBy(s => s.FirstUse).Select(s => s.Step)];
        if (definition.Steps.Count > 0)
        {
            foreach (var field in fields.Where(f => f.StepName is null))
                field.StepName = definition.Steps[0].Name;
        }
        return definition;
    }

    private static WebFieldDefinition? BuildField(PropertyInfo property, int index, int depth)
    {
        var valueType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        var (type, element) = Infer(valueType);
        if (type is null)
            return null;

        var display = property.GetCustomAttribute<DisplayAttribute>();
        var field = new WebFieldDefinition
        {
            Name = property.Name,
            Property = property,
            ValueType = valueType,
            ElementType = element,
            Type = type.Value,
            Label = display?.GetName() ?? Humanize(property.Name),
            Placeholder = display?.GetPrompt(),
            Help = display?.GetDescription(),
            Order = display?.GetOrder() ?? index * 10
        };

        if (type == WebFieldType.Repeater)
        {
            if (depth >= MaxDepth)
                return null;
            field.Repeater = Build(element!, depth + 1);
        }
        if (valueType.IsEnum || (element?.IsEnum ?? false))
            field.Options = EnumOptions.For(valueType.IsEnum ? valueType : element!);

        ApplyAnnotations(field, property);
        ApplyWebAttributes(field, property);
        return field;
    }

    private static void ApplyAnnotations(WebFieldDefinition field, PropertyInfo property)
    {
        var others = new List<ValidationAttribute>();
        foreach (var attribute in property.GetCustomAttributes<ValidationAttribute>(true))
        {
            switch (attribute)
            {
                case RequiredAttribute: field.Required = true; break;
                case StringLengthAttribute length:
                    field.MaxLength = length.MaximumLength;
                    if (length.MinimumLength > 0) field.MinLength = length.MinimumLength;
                    break;
                case MinLengthAttribute min when field.ElementType is null: field.MinLength = min.Length; break;
                case MaxLengthAttribute max when field.ElementType is null: field.MaxLength = max.Length; break;
                case RangeAttribute range:
                    field.Min = Invariant(range.Minimum);
                    field.Max = Invariant(range.Maximum);
                    break;
                case RegularExpressionAttribute regex:
                    field.Pattern = regex.Pattern;
                    field.PatternMessage = regex.ErrorMessage;
                    break;
                case CompareAttribute compare: field.EqualTo = compare.OtherProperty; break;
                case EmailAddressAttribute: field.Type = WebFieldType.Email; break;
                case UrlAttribute: field.Type = WebFieldType.Url; break;
                case PhoneAttribute: field.Type = WebFieldType.Tel; break;
                case DataTypeAttribute dataType: ApplyDataType(field, dataType.DataType); break;
                default: others.Add(attribute); break;
            }
        }
        field.Validators = others;
    }

    private static void ApplyDataType(WebFieldDefinition field, DataType dataType) => field.Type = dataType switch
    {
        DataType.Password => WebFieldType.Password,
        DataType.MultilineText => WebFieldType.TextArea,
        DataType.EmailAddress => WebFieldType.Email,
        DataType.PhoneNumber => WebFieldType.Tel,
        DataType.Url => WebFieldType.Url,
        DataType.Date => WebFieldType.Date,
        DataType.Time => WebFieldType.Time,
        DataType.DateTime => WebFieldType.DateTimeLocal,
        DataType.Html => WebFieldType.RichText,
        _ => field.Type
    };

    private static void ApplyWebAttributes(WebFieldDefinition field, PropertyInfo property)
    {
        if (property.GetCustomAttribute<WebFieldAttribute>() is { } web)
        {
            if (web.HasType) field.Type = web.Type;
            if (web.Label is not null) field.Label = web.Label;
            if (web.Placeholder is not null) field.Placeholder = web.Placeholder;
            if (web.Help is not null) field.Help = web.Help;
            if (web.Autocomplete is not null) field.Autocomplete = web.Autocomplete;
            if (web.Order != int.MinValue) field.Order = web.Order;
            if (web.Step is not null) field.StepValue = web.Step;
            field.ReadOnly = web.ReadOnly;
            field.Remote = web.Remote;
        }
        if (property.GetCustomAttribute<WebOptionsAttribute>() is { } options)
        {
            if (options.Options.Count > 0)
                field.Options = [.. options.Options.Select(ParseOption)];
            field.OptionsProvider = options.Provider;
            if (field.Type == WebFieldType.Text)
                field.Type = WebFieldType.Select;
        }
        if (property.GetCustomAttribute<WebShowWhenAttribute>() is { } show)
            field.ShowWhen = new WebShowWhen(show.Field, show.Values);
        if (property.GetCustomAttribute<WebFileAttribute>() is { } file)
        {
            field.File = new WebFileRules
            {
                MaxBytes = file.MaxBytes > 0 ? file.MaxBytes : null,
                Extensions = string.IsNullOrWhiteSpace(file.Extensions) ? [] : [.. file.Extensions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(NormalizeExtension)],
                MaxFiles = field.ElementType is null ? 1 : Math.Max(1, file.MaxFiles),
                ImagesOnly = file.ImagesOnly,
                MaxWidth = file.MaxWidth > 0 ? file.MaxWidth : null,
                MaxHeight = file.MaxHeight > 0 ? file.MaxHeight : null
            };
        }
        else if (field.Type == WebFieldType.File)
        {
            field.File = new WebFileRules { MaxFiles = field.ElementType is null ? 1 : 10 };
        }
        if (property.GetCustomAttribute<WebRepeaterAttribute>() is { } repeater)
        {
            field.RepeaterMin = repeater.Min;
            field.RepeaterMax = repeater.Max;
        }
    }

    /// <summary>Infers the field type; null for unsupported property types.</summary>
    internal static (WebFieldType? Type, Type? Element) Infer(Type type)
    {
        if (type == typeof(string)) return (WebFieldType.Text, null);
        if (type == typeof(bool)) return (WebFieldType.Checkbox, null);
        if (WebValueConverter.IsNumeric(type)) return (WebFieldType.Number, null);
        if (type == typeof(DateOnly)) return (WebFieldType.Date, null);
        if (type == typeof(DateTime) || type == typeof(DateTimeOffset)) return (WebFieldType.DateTimeLocal, null);
        if (type == typeof(TimeOnly) || type == typeof(TimeSpan)) return (WebFieldType.Time, null);
        if (type == typeof(Guid)) return (WebFieldType.Text, null);
        if (type.IsEnum) return (WebFieldType.Select, null);
        if (type == typeof(WebUploadedFile)) return (WebFieldType.File, null);

        var element = WebValueConverter.CollectionElement(type);
        if (element is null) return (null, null);
        if (element == typeof(WebUploadedFile)) return (WebFieldType.File, element);
        if (element == typeof(string) || element.IsEnum || WebValueConverter.IsNumeric(element) || element == typeof(Guid))
            return (WebFieldType.CheckboxList, element);
        if (element.IsClass && element.GetConstructor(Type.EmptyTypes) is not null)
            return (WebFieldType.Repeater, element);
        return (null, null);
    }

    /// <summary><c>ContactForm</c> → <c>contact</c>, <c>NewsletterSignupModel</c> → <c>newsletter-signup</c>.</summary>
    public static string DefaultId(Type type)
    {
        var name = type.Name;
        foreach (var suffix in (string[])["FormModel", "Form", "Model"])
        {
            if (name.Length > suffix.Length && name.EndsWith(suffix, StringComparison.Ordinal))
            {
                name = name[..^suffix.Length];
                break;
            }
        }
        var builder = new StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]) && i > 0) builder.Append('-');
            builder.Append(char.ToLowerInvariant(name[i]));
        }
        return builder.ToString();
    }

    /// <summary><c>FirstName</c> → <c>First name</c>.</summary>
    public static string Humanize(string name)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(name[i - 1])) builder.Append(' ').Append(char.ToLowerInvariant(c));
            else if (c is '_' or '-') builder.Append(' ');
            else builder.Append(i == 0 ? char.ToUpperInvariant(c) : c);
        }
        return builder.ToString();
    }

    private static WebFieldOption ParseOption(string option)
    {
        var bar = option.IndexOf('|', StringComparison.Ordinal);
        return bar < 0 ? new WebFieldOption(option, option) : new WebFieldOption(option[..bar], option[(bar + 1)..]);
    }

    private static string NormalizeExtension(string extension) =>
        (extension.StartsWith('.') ? extension : "." + extension).ToLowerInvariant();

    private static string? Invariant(object? value) => value switch
    {
        null => null,
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString()
    };
}

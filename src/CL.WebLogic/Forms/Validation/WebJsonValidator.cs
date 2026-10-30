using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace CL.WebLogic.Forms.Validation;

/// <summary>
/// Validates a deserialized JSON model with its DataAnnotations attributes, recursing into nested
/// objects, collections and dictionaries, then <see cref="IValidatableObject"/> per object.
/// Only members the JSON contract knows are looked at (ignored members are never bound, so never
/// validated). Error keys are the JSON property names of the given options, with dotted and indexed
/// paths for nested values (<c>items[0].name</c>); the empty key holds model-level errors.
/// </summary>
public sealed class WebJsonValidator
{
    /// <summary>Default nesting limit; deeper values get an error instead of being walked.</summary>
    public const int DefaultMaxDepth = 32;

    private readonly WebFormMessages _messages;

    /// <summary>Creates the validator.</summary>
    public WebJsonValidator(WebFormMessages messages) => _messages = messages ?? throw new ArgumentNullException(nameof(messages));

    /// <summary>
    /// Validates <paramref name="model"/> and returns the errors by JSON path (empty when valid).
    /// </summary>
    /// <param name="model">The model to validate.</param>
    /// <param name="options">The JSON options the model was read with (they decide the property names). Made read-only, as serializing with them would.</param>
    /// <param name="services">Optional services for <see cref="ValidationContext"/> (custom attributes).</param>
    /// <param name="maxDepth">Nesting limit; an object or collection nested deeper is reported as not valid.</param>
    public IReadOnlyDictionary<string, string[]> Validate(object model, JsonSerializerOptions options, IServiceProvider? services = null, int maxDepth = DefaultMaxDepth)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegative(maxDepth);

        // Serializing or deserializing locks the options the same way; reading metadata needs it.
        if (!options.IsReadOnly)
            options.MakeReadOnly(populateMissingResolver: true);

        var walk = new Walk(options, services, maxDepth);
        Visit(walk, model, string.Empty, 0);
        return walk.Errors.ToDictionary(e => e.Key, e => e.Value.ToArray(), StringComparer.Ordinal);
    }

    /// <summary>The label for a path: its last member name (<c>items[0].name</c> gives <c>name</c>).</summary>
    internal static string LabelOf(string path)
    {
        var dot = path.LastIndexOf('.');
        var last = dot >= 0 ? path[(dot + 1)..] : path;
        var bracket = last.IndexOf('[', StringComparison.Ordinal);
        return bracket > 0 ? last[..bracket] : last;
    }

    private void Visit(Walk walk, object value, string path, int depth)
    {
        var info = walk.Options.GetTypeInfo(value.GetType());
        if (info.Kind == JsonTypeInfoKind.None)
            return;

        if (depth > walk.MaxDepth)
        {
            walk.Add(path, _messages.Format("invalid", LabelOf(path)));
            return;
        }

        // A value already being validated higher up (a cycle) is skipped, not walked forever.
        if (!walk.Visiting.Add(value))
            return;
        try
        {
            switch (info.Kind)
            {
                case JsonTypeInfoKind.Object:
                    VisitObject(walk, info, value, path, depth);
                    break;
                case JsonTypeInfoKind.Dictionary when value is IDictionary dictionary:
                    foreach (DictionaryEntry entry in dictionary)
                    {
                        if (entry.Value is not null)
                            Visit(walk, entry.Value, $"{path}[{entry.Key}]", depth + 1);
                    }
                    break;
                case JsonTypeInfoKind.Enumerable when value is IEnumerable items:
                    var index = 0;
                    foreach (var item in items)
                    {
                        if (item is not null)
                            Visit(walk, item, $"{path}[{index}]", depth + 1);
                        index++;
                    }
                    break;
            }
        }
        finally
        {
            walk.Visiting.Remove(value);
        }
    }

    private void VisitObject(Walk walk, JsonTypeInfo info, object model, string path, int depth)
    {
        var before = walk.Count;
        foreach (var property in info.Properties)
        {
            if (property.Get is null || property.AttributeProvider is not MemberInfo member)
                continue;

            var key = path.Length == 0 ? property.Name : path + "." + property.Name;
            var label = member.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? property.Name;
            var value = property.Get(model);
            var failed = false;
            foreach (var attribute in member.GetCustomAttributes<ValidationAttribute>(inherit: true))
            {
                var context = new ValidationContext(model, walk.Services, null) { MemberName = member.Name, DisplayName = label };
                var result = attribute.GetValidationResult(value, context);
                if (result == ValidationResult.Success)
                    continue;
                failed = true;
                walk.Add(key, Message(attribute, result, label));
            }

            if (!failed && value is not null)
                Visit(walk, value, key, depth + 1);
        }

        // Like DataAnnotations: object-level rules only run once the members are valid.
        if (walk.Count != before)
            return;

        var objectContext = new ValidationContext(model, walk.Services, null);
        var modelLabel = path.Length == 0 ? model.GetType().Name : LabelOf(path);
        foreach (var attribute in model.GetType().GetCustomAttributes<ValidationAttribute>(inherit: true))
        {
            var result = attribute.GetValidationResult(model, objectContext);
            if (result != ValidationResult.Success)
                AddResult(walk, info, path, result, modelLabel);
        }

        if (walk.Count == before && model is IValidatableObject validatable)
        {
            foreach (var result in validatable.Validate(objectContext) ?? [])
            {
                if (result != ValidationResult.Success)
                    AddResult(walk, info, path, result, modelLabel);
            }
        }
    }

    private void AddResult(Walk walk, JsonTypeInfo info, string path, ValidationResult? result, string label)
    {
        var message = result?.ErrorMessage ?? _messages.Format("invalid", label);
        var members = result?.MemberNames.Where(m => !string.IsNullOrEmpty(m)).ToList() ?? [];
        if (members.Count == 0)
        {
            walk.Add(path, message);
            return;
        }

        foreach (var member in members)
        {
            var name = JsonName(info, member, walk.Options);
            walk.Add(path.Length == 0 ? name : path + "." + name, message);
        }
    }

    private string Message(ValidationAttribute attribute, ValidationResult? result, string label)
    {
        // [Required] without its own message gets the localizable forms text, like form fields.
        if (attribute is RequiredAttribute && attribute.ErrorMessage is null && attribute.ErrorMessageResourceName is null)
            return _messages.Format("required", label);
        return result?.ErrorMessage ?? _messages.Format("invalid", label);
    }

    private static string JsonName(JsonTypeInfo info, string member, JsonSerializerOptions options)
    {
        foreach (var property in info.Properties)
        {
            if (property.AttributeProvider is MemberInfo m && string.Equals(m.Name, member, StringComparison.Ordinal))
                return property.Name;
        }

        return options.PropertyNamingPolicy?.ConvertName(member) ?? member;
    }

    private sealed class Walk(JsonSerializerOptions options, IServiceProvider? services, int maxDepth)
    {
        public JsonSerializerOptions Options { get; } = options;

        public IServiceProvider? Services { get; } = services;

        public int MaxDepth { get; } = maxDepth;

        public HashSet<object> Visiting { get; } = new(ReferenceEqualityComparer.Instance);

        public Dictionary<string, List<string>> Errors { get; } = new(StringComparer.Ordinal);

        public int Count { get; private set; }

        public void Add(string key, string message)
        {
            if (!Errors.TryGetValue(key, out var list))
                Errors[key] = list = [];
            if (list.Contains(message))
                return;
            list.Add(message);
            Count++;
        }
    }
}

using CL.WebLogic.Forms.Models;
using CL.WebLogic.Forms.Validation;

namespace CL.WebLogic.Forms.Rendering;

/// <summary>The client validation schema embedded in rendered forms (the client's <c>validation.js</c> reads it).</summary>
public static class WebFormSchema
{
    private static readonly Dictionary<string, string> SecondArgument = new(StringComparer.Ordinal)
    {
        ["minLength"] = "{minLength}",
        ["maxLength"] = "{maxLength}",
        ["min"] = "{min}",
        ["max"] = "{max}",
        ["equalTo"] = "{otherLabel}"
    };

    /// <summary>The schema object (serialize with the default JSON encoder).</summary>
    public static object Build(WebFormDefinition definition, IEnumerable<WebFieldDefinition> fields, WebFormMessages messages)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(messages);
        var clientMessages = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var key in (string[])["required", "minLength", "maxLength", "min", "max", "number", "pattern", "email", "url", "equalTo"])
        {
            var template = messages.Template(key).Replace("{0}", "{label}", StringComparison.Ordinal);
            clientMessages[key] = SecondArgument.TryGetValue(key, out var second) ? template.Replace("{1}", second, StringComparison.Ordinal) : template;
        }

        return new Dictionary<string, object?>
        {
            ["formId"] = definition.Id,
            ["messages"] = clientMessages,
            ["fields"] = fields
                .Where(f => f.Repeater is null && f.Type is not (WebFieldType.File or WebFieldType.Hidden))
                .Select(f => Field(definition, f))
                .ToList()
        };
    }

    private static Dictionary<string, object?> Field(WebFormDefinition definition, WebFieldDefinition field)
    {
        var schema = new Dictionary<string, object?>
        {
            ["name"] = field.Name,
            ["label"] = field.Label,
            ["type"] = WebFormRenderer.InputType(field.Type)
        };
        if (field.Required) schema["required"] = true;
        if (field.MinLength is not null) schema["minLength"] = field.MinLength;
        if (field.MaxLength is not null) schema["maxLength"] = field.MaxLength;
        if (field.Min is not null) schema["min"] = field.Min;
        if (field.Max is not null) schema["max"] = field.Max;
        if (field.Pattern is not null) schema["pattern"] = field.Pattern;
        if (field.PatternMessage is not null) schema["messages"] = new Dictionary<string, string> { ["pattern"] = field.PatternMessage };
        if (field.EqualTo is not null)
        {
            schema["equalTo"] = field.EqualTo;
            schema["equalToLabel"] = definition.Field(field.EqualTo)?.Label ?? field.EqualTo;
        }
        if (field.Remote) schema["remote"] = true;
        if (field.ShowWhen is not null) schema["showWhen"] = new { field = field.ShowWhen.Field, values = field.ShowWhen.Values };
        return schema;
    }
}

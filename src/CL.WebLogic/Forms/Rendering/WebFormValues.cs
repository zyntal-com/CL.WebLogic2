using CL.WebLogic.Forms.Models;
using CL.WebLogic.Forms.Services;
using CL.WebLogic.Forms.Uploads;
using Microsoft.Extensions.Primitives;

namespace CL.WebLogic.Forms.Rendering;

/// <summary>Turns a model into the raw values a form is rendered with.</summary>
public static class WebFormValues
{
    /// <summary>Values of <paramref name="model"/> (passwords and files are never included).</summary>
    public static Dictionary<string, StringValues> FromModel(WebFormDefinition definition, object? model, string prefix = "")
    {
        var values = new Dictionary<string, StringValues>(StringComparer.Ordinal);
        if (model is null)
            return values;
        foreach (var field in definition.Fields)
        {
            if (field.Type is WebFieldType.Password or WebFieldType.File)
                continue;
            var key = prefix + field.Name;
            var value = field.Property.GetValue(model);
            if (field.Repeater is not null && value is System.Collections.IEnumerable items)
            {
                var index = 0;
                foreach (var item in items)
                {
                    foreach (var pair in FromModel(field.Repeater, item, $"{key}[{index}]."))
                        values[pair.Key] = pair.Value;
                    index++;
                }
                continue;
            }
            if (value is System.Collections.IEnumerable many and not string)
                values[key] = new StringValues([.. many.Cast<object?>().Where(v => v is not WebUploadedFile).Select(WebValueConverter.Format)]);
            else if (value is not null)
                values[key] = WebValueConverter.Format(value);
        }
        return values;
    }
}

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CL.WebLogic.Runtime.Services;

/// <summary>JSON serializer settings used across the toolkit (camelCase, compact, nulls omitted).</summary>
public static class WebJson
{
    /// <summary>Shared options for toolkit-owned payloads (never for plugin types — see <see cref="CreateOptions"/>).</summary>
    public static JsonSerializerOptions Default { get; } = CreateOptions();

    /// <summary>Creates a fresh options instance with the toolkit conventions.</summary>
    public static JsonSerializerOptions CreateOptions() => new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
        // Keep the default encoder: it escapes <, >, &, ' and " so JSON embedded in HTML stays inert.
        Encoder = JavaScriptEncoder.Default,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
}

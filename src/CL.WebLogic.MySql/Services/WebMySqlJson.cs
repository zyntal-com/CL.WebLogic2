using System.Text.Json;

namespace CL.WebLogic.MySql.Services;

/// <summary>JSON settings for the record columns (web defaults, so field names stay stable).</summary>
internal static class WebMySqlJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T Read<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options) ?? throw new InvalidOperationException($"MySQL store: an empty {typeof(T).Name} record.");
}

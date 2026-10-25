using System.Globalization;

namespace Samples.Hosting;

/// <summary>Typed reads of the environment variables a sample container is configured with.</summary>
public static class DemoEnvironment
{
    /// <summary>The trimmed value, or null when unset or blank.</summary>
    public static string? Text(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary><c>true</c>/<c>false</c>/<c>1</c>/<c>0</c>, or null when unset.</summary>
    public static bool? Flag(string name) => Text(name)?.ToLowerInvariant() switch
    {
        null => null,
        "1" or "true" or "yes" or "on" => true,
        "0" or "false" or "no" or "off" => false,
        var other => throw new InvalidOperationException($"{name}='{other}' is not a boolean.")
    };

    /// <summary>An integer, or null when unset.</summary>
    public static int? Number(string name) =>
        Text(name) is { } text
            ? int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : throw new InvalidOperationException($"{name}='{text}' is not a number.")
            : null;

    /// <summary>A comma-separated list, or null when unset.</summary>
    public static IReadOnlyList<string>? List(string name) =>
        Text(name)?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

using System.Text.Json;
using System.Text.Json.Nodes;

namespace Samples.Hosting;

/// <summary>
/// Sets individual values in a CodeLogic JSON config file before CodeLogic loads it, so a
/// container can be configured with environment variables. Only the given keys change; the
/// rest of the file (and edits made to it on the volume) is kept. Paths are camelCase, as
/// CodeLogic writes them: <c>security.forwardedHeaders.enabled</c>.
/// </summary>
public sealed class ConfigFilePatch
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    private readonly string _path;
    private readonly JsonObject _root;
    private bool _changed;

    private ConfigFilePatch(string path, JsonObject root)
    {
        _path = path;
        _root = root;
    }

    /// <summary>Opens (or starts) the file at <paramref name="path"/>.</summary>
    public static ConfigFilePatch Open(string path)
    {
        var root = File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path)) as JsonObject : null;
        return new ConfigFilePatch(path, root ?? []);
    }

    /// <summary>Sets a string; null leaves the key alone.</summary>
    public ConfigFilePatch Set(string path, string? value) => value is null ? this : Put(path, JsonValue.Create(value));

    /// <summary>Sets a boolean; null leaves the key alone.</summary>
    public ConfigFilePatch Set(string path, bool? value) => value is null ? this : Put(path, JsonValue.Create(value.Value));

    /// <summary>Sets a number; null leaves the key alone.</summary>
    public ConfigFilePatch Set(string path, int? value) => value is null ? this : Put(path, JsonValue.Create(value.Value));

    /// <summary>Sets a string list; null leaves the key alone.</summary>
    public ConfigFilePatch Set(string path, IReadOnlyList<string>? values) =>
        values is null ? this : Put(path, new JsonArray([.. values.Select(v => (JsonNode?)JsonValue.Create(v))]));

    /// <summary>Sets a value only when the file does not have the key yet (defaults the app relies on).</summary>
    public ConfigFilePatch Default(string path, string value) => Find(path) is null ? Put(path, JsonValue.Create(value)) : this;

    /// <summary>Sets a boolean only when the file does not have the key yet.</summary>
    public ConfigFilePatch Default(string path, bool value) => Find(path) is null ? Put(path, JsonValue.Create(value)) : this;

    /// <summary>Writes the file when anything changed.</summary>
    public void Save()
    {
        if (!_changed)
            return;
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temp = _path + ".tmp";
        File.WriteAllText(temp, _root.ToJsonString(WriteOptions));
        File.Move(temp, _path, overwrite: true);
    }

    private JsonNode? Find(string path)
    {
        JsonNode? node = _root;
        foreach (var segment in path.Split('.'))
            node = (node as JsonObject)?[segment];
        return node;
    }

    private ConfigFilePatch Put(string path, JsonNode? value)
    {
        var segments = path.Split('.');
        var parent = _root;
        foreach (var segment in segments[..^1])
        {
            if (parent[segment] is not JsonObject child)
            {
                child = [];
                parent[segment] = child;
            }
            parent = child;
        }
        parent[segments[^1]] = value;
        _changed = true;
        return this;
    }
}

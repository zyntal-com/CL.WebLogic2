using System.Reflection;
using System.Text.Json;
using CL.WebLogic.Client.Models;

namespace CL.WebLogic.Client.Services;

/// <summary>
/// The client files embedded in the WebLogic assembly by the build (bundles, CSS, their Brotli
/// and gzip variants and <c>client-manifest.json</c>). Loaded once.
/// </summary>
public sealed class ClientAssetCatalog
{
    /// <summary>URL prefix of the client files.</summary>
    public const string PathPrefix = "/_weblogic/client/";

    private const string ResourcePrefix = "CL.WebLogic.Client.";

    private readonly Dictionary<string, ClientAsset> _byName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ClientAsset> _byHashedName = new(StringComparer.Ordinal);

    /// <summary>Loads the embedded files of <paramref name="assembly"/> (default: the WebLogic assembly).</summary>
    public ClientAssetCatalog(Assembly? assembly = null)
    {
        assembly ??= typeof(ClientAssetCatalog).Assembly;
        var manifestBytes = Read(assembly, "client-manifest.json");
        if (manifestBytes is null)
            return;

        using var manifest = JsonDocument.Parse(manifestBytes);
        Version = manifest.RootElement.TryGetProperty("version", out var version) ? version.GetString() ?? "" : "";
        foreach (var file in manifest.RootElement.GetProperty("files").EnumerateArray())
        {
            var name = file.GetProperty("name").GetString()!;
            var hash = file.GetProperty("hash").GetString()!;
            var content = Read(assembly, name);
            if (content is null)
                continue;
            var asset = new ClientAsset
            {
                Name = name,
                HashedName = HashName(name, hash),
                ContentType = file.GetProperty("contentType").GetString()!,
                Hash = hash,
                Integrity = file.GetProperty("integrity").GetString()!,
                Content = content,
                Brotli = Read(assembly, name + ".br"),
                Gzip = Read(assembly, name + ".gz")
            };
            _byName[name] = asset;
            _byHashedName[asset.HashedName] = asset;
        }
    }

    /// <summary>Client version (the package version at build time).</summary>
    public string Version { get; } = "";

    /// <summary>All files.</summary>
    public IReadOnlyCollection<ClientAsset> Assets => _byName.Values;

    /// <summary>A file by logical name.</summary>
    public ClientAsset? Get(string name) => _byName.GetValueOrDefault(name);

    /// <summary>Resolves a requested file name: fingerprinted (cacheable forever) or logical.</summary>
    public bool TryResolve(string? requested, out ClientAsset asset, out bool fingerprinted)
    {
        fingerprinted = false;
        asset = null!;
        if (string.IsNullOrEmpty(requested))
            return false;
        if (_byHashedName.TryGetValue(requested, out var hashed))
        {
            asset = hashed;
            fingerprinted = true;
            return true;
        }
        if (_byName.TryGetValue(requested, out var plain))
        {
            asset = plain;
            return true;
        }
        return false;
    }

    /// <summary>The fingerprinted URL path of a file (without PathBase), or null when it does not exist.</summary>
    public string? UrlOf(string name) => _byName.TryGetValue(name, out var asset) ? PathPrefix + asset.HashedName : null;

    /// <summary><c>weblogic.esm.js</c> + <c>abc</c> → <c>weblogic.esm.abc.js</c>.</summary>
    public static string HashName(string name, string hash)
    {
        var dot = name.LastIndexOf('.');
        return dot < 0 ? name + "." + hash : name[..dot] + "." + hash + name[dot..];
    }

    private static byte[]? Read(Assembly assembly, string name)
    {
        using var stream = assembly.GetManifestResourceStream(ResourcePrefix + name);
        if (stream is null)
            return null;
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}

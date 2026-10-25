using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CL.WebLogic.ClientBundler.Linking;

namespace CL.WebLogic.ClientBundler.Output;

/// <summary>
/// Builds every bundle and asset in <c>bundle.json</c>, writes each with Brotli and gzip
/// variants, and writes <c>client-manifest.json</c> (content hash, SRI integrity, type).
/// </summary>
internal sealed partial class BundlerRun
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly string _source;
    private readonly string _output;
    private readonly string _version;

    /// <summary>Creates a run.</summary>
    public BundlerRun(string source, string output, string version)
    {
        _source = Path.GetFullPath(source);
        _output = Path.GetFullPath(output);
        _version = version;
    }

    /// <summary>Runs the build; returns the written file names.</summary>
    public IReadOnlyList<string> Execute()
    {
        var configPath = Path.Combine(_source, "bundle.json");
        if (!File.Exists(configPath))
            throw new BundleException("bundle.json not found", configPath, 0);
        var config = JsonSerializer.Deserialize<BundleConfig>(File.ReadAllText(configPath), Json)
            ?? throw new BundleException("bundle.json is empty", configPath, 0);
        var header = config.Header.Replace("{version}", _version, StringComparison.Ordinal);

        Directory.CreateDirectory(_output);
        foreach (var stale in Directory.GetFiles(_output))
            File.Delete(stale);

        var manifest = new List<ManifestEntry>();
        foreach (var bundle in config.Bundles)
        {
            foreach (var format in bundle.Formats)
            {
                var kind = format.ToLowerInvariant() switch
                {
                    "iife" => BundleFormat.Iife,
                    "esm" => BundleFormat.Esm,
                    _ => throw new BundleException($"unknown format '{format}'", configPath, 0)
                };
                var code = new BundleLinker(ReadModule).Link(bundle.Entry, kind, header);
                var name = bundle.Name + (kind == BundleFormat.Esm ? ".esm.js" : ".js");
                manifest.Add(Write(name, code, "text/javascript; charset=utf-8"));
            }
        }

        foreach (var asset in config.Assets)
        {
            var text = ReadModule(asset.Source) ?? throw new BundleException($"asset '{asset.Source}' not found", configPath, 0);
            var css = CssComment().Replace(text.Replace("\r\n", "\n", StringComparison.Ordinal), "");
            manifest.Add(Write(asset.Name, (header.Length > 0 ? "/*! " + header + " */\n" : "") + css.Trim() + "\n", "text/css; charset=utf-8"));
        }

        File.WriteAllText(Path.Combine(_output, "client-manifest.json"),
            JsonSerializer.Serialize(new { version = _version, files = manifest }, Json));
        return [.. manifest.Select(m => m.Name), "client-manifest.json"];
    }

    private string? ReadModule(string relativePath)
    {
        var full = Path.GetFullPath(Path.Combine(_source, relativePath));
        if (!full.StartsWith(_source + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return null;
        return File.Exists(full) ? File.ReadAllText(full) : null;
    }

    private ManifestEntry Write(string name, string content, string contentType)
    {
        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(content);
        var path = Path.Combine(_output, name);
        File.WriteAllBytes(path, bytes);

        using (var br = new BrotliStream(File.Create(path + ".br"), CompressionLevel.SmallestSize))
            br.Write(bytes);
        using (var gz = new GZipStream(File.Create(path + ".gz"), CompressionLevel.SmallestSize))
            gz.Write(bytes);

        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes))[..12];
        var integrity = "sha384-" + Convert.ToBase64String(SHA384.HashData(bytes));
        return new ManifestEntry(name, hash, contentType, integrity, bytes.Length);
    }

    [GeneratedRegex(@"/\*(?!!)[\s\S]*?\*/", RegexOptions.None, 1000)]
    private static partial Regex CssComment();
}

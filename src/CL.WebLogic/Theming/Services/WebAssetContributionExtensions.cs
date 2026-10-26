using System.Reflection;
using System.Security.Cryptography;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Routing.Services;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;

namespace CL.WebLogic.Theming.Services;

/// <summary>Lets contributors and plugins ship static files (CSS, JS, images) inside their assembly.</summary>
public static class WebAssetContributionExtensions
{
    private const long MaxBytesPerSet = 64L * 1024 * 1024;
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    /// <summary>
    /// Serves every embedded resource of <paramref name="assembly"/> whose name starts with
    /// <paramref name="resourcePrefix"/> at <c>/_weblogic/plugins/{name}/{rest of the name}</c>, where
    /// <paramref name="name"/> defaults to the contributor id. Only extensions in
    /// <c>Theme.AllowedAssetExtensions</c> are served; dot-files and unsafe paths are skipped. In
    /// templates, <c>{asset:name:path}</c> gives a fingerprinted URL, and a theme file at
    /// <c>{AssetsFolder}/plugins/{name}/{path}</c> overrides the plugin's file. Embed with
    /// <code>&lt;EmbeddedResource Include="assets\**\*" LogicalName="assets/%(RecursiveDir)%(Filename)%(Extension)" /&gt;</code>
    /// </summary>
    /// <returns>The paths registered.</returns>
    public static IReadOnlyList<string> MapAssets(this WebContributionBuilder builder, Assembly assembly, string resourcePrefix = "assets/", string? name = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentException.ThrowIfNullOrEmpty(resourcePrefix);
        name ??= builder.Contributor.Id.ToLowerInvariant();
        if (!WebContributedAssets.IsValidName(name))
            throw new ArgumentException($"'{name}' is not a valid asset name (lower-case letters, digits, '.', '-', '_'); pass one explicitly.", nameof(name));

        var allowed = builder.Services.GetRequiredService<WebLogicConfigHolder>().Current.Theme.AllowedAssetExtensions;
        var prefix = resourcePrefix.Replace('\\', '/');
        var registered = DateTimeOffset.UtcNow;
        var files = new Dictionary<string, WebContributedAsset>(StringComparer.Ordinal);
        long total = 0;
        foreach (var resource in assembly.GetManifestResourceNames())
        {
            var logical = resource.Replace('\\', '/');
            if (!logical.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;
            var path = logical[prefix.Length..];
            var extension = Path.GetExtension(path);
            if (!ThemeAssetResolver.IsSafeRelativePath(path) || !allowed.Contains(extension, StringComparer.OrdinalIgnoreCase))
                continue;

            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            var bytes = buffer.ToArray();
            total += bytes.Length;
            if (total > MaxBytesPerSet)
                throw new InvalidOperationException($"{assembly.GetName().Name} embeds more than {MaxBytesPerSet / 1024 / 1024} MB of assets under '{prefix}'; serve large files from storage instead.");
            if (!ContentTypes.TryGetContentType(path, out var contentType))
                contentType = "application/octet-stream";
            files[path] = new WebContributedAsset(bytes, contentType, Convert.ToHexStringLower(SHA256.HashData(bytes).AsSpan(0, 8)), registered);
        }
        if (files.Count == 0)
            throw new InvalidOperationException(
                $"{assembly.GetName().Name} has no servable embedded assets under '{prefix}'. Embed them with " +
                @"<EmbeddedResource Include=""assets\**\*"" LogicalName=""assets/%(RecursiveDir)%(Filename)%(Extension)"" />.");

        var registry = builder.Services.GetRequiredService<WebContributedAssets>();
        var owner = builder.Contributor;
        builder.OnCommit(() => registry.Add(owner, name, files));
        builder.OnRemove(() =>
        {
            registry.Remove(owner);
            return ValueTask.CompletedTask;
        });
        return [.. files.Keys];
    }
}

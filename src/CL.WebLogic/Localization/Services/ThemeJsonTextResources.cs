using System.Collections.Concurrent;
using System.Text.Json;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Theming.Services;

namespace CL.WebLogic.Localization.Services;

/// <summary>
/// Built-in <see cref="IWebTextResources"/>: one JSON file per culture in the theme,
/// <c>{theme}/{Localization.ResourcesFolder}/{culture}.json</c> (default <c>locales/sv-SE.json</c>).
/// Nested objects are flattened with dots, so <c>{"forms":{"required":"…"}}</c> is the key
/// <c>forms.required</c>; values that are not strings are ignored. Files are cached and re-checked
/// at most every <c>Theme.TemplateChangeCheckSeconds</c> (timestamp and length), so theme
/// updates are picked up without a restart. A file that does not parse keeps the last good texts.
/// Paths are contained to the theme; links and files over 4 MiB are refused.
/// </summary>
public sealed class ThemeJsonTextResources : IWebTextResources
{
    private const long MaxFileBytes = 4 * 1024 * 1024;
    private const int MaxCachedCultures = 256;

    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static readonly JsonDocumentOptions ParseOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
        MaxDepth = 32
    };

    private static readonly IReadOnlyDictionary<string, string> Empty = new Dictionary<string, string>();

    private readonly ConcurrentDictionary<string, Entry> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly IWebThemeRoot _root;
    private readonly WebLogicConfigHolder _config;
    private readonly IWebLogicContext _context;
    private readonly TimeProvider _time;

    /// <summary>Creates the resources.</summary>
    public ThemeJsonTextResources(IWebThemeRoot root, WebLogicConfigHolder config, IWebLogicContext context, TimeProvider? time = null)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _time = time ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public string? GetText(string culture, string key)
    {
        if (string.IsNullOrEmpty(key) || !IsCultureName(culture))
            return null;
        return Load(culture).TryGetValue(key, out var text) ? text : null;
    }

    /// <summary>Drops the cached texts so the next lookup reads the files again.</summary>
    public void Invalidate() => _cache.Clear();

    private IReadOnlyDictionary<string, string> Load(string culture)
    {
        var now = _time.GetUtcNow();
        var checkSeconds = _config.Current.Theme.TemplateChangeCheckSeconds;
        if (_cache.TryGetValue(culture, out var cached)
            && (checkSeconds == 0 || now - cached.CheckedAt < TimeSpan.FromSeconds(checkSeconds)))
            return cached.Texts;

        var file = Resolve(culture);
        if (file is null)
            return Store(culture, new Entry(Empty, null, DateTime.MinValue, -1, now));

        if (cached is not null && cached.FilePath == file.FullName && cached.LastWriteUtc == file.LastWriteTimeUtc && cached.Length == file.Length)
            return Store(culture, cached with { CheckedAt = now });

        var texts = Read(file, cached is not null && cached.FilePath == file.FullName ? cached.Texts : Empty);
        return Store(culture, new Entry(texts, file.FullName, file.LastWriteTimeUtc, file.Length, now));
    }

    private IReadOnlyDictionary<string, string> Store(string culture, Entry entry)
    {
        if (_cache.Count >= MaxCachedCultures && !_cache.ContainsKey(culture))
            _cache.Clear();
        _cache[culture] = entry;
        return entry.Texts;
    }

    private FileInfo? Resolve(string culture)
    {
        var root = Path.GetFullPath(_root.RootPath);
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        var folder = _config.Current.Localization.ResourcesFolder.Replace('/', Path.DirectorySeparatorChar);
        var full = Path.GetFullPath(Path.Combine(root, folder, culture + ".json"));
        if (!full.StartsWith(rootWithSeparator, PathComparison))
            return null;

        var file = new FileInfo(full);
        return file.Exists && file.LinkTarget is null ? file : null;
    }

    private IReadOnlyDictionary<string, string> Read(FileInfo file, IReadOnlyDictionary<string, string> previous)
    {
        if (file.Length > MaxFileBytes)
        {
            _context.Logger.Warning($"Text resource file {file.Name} is larger than {MaxFileBytes} bytes and was ignored.");
            return previous;
        }
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(file.FullName), ParseOptions);
            var texts = new Dictionary<string, string>(StringComparer.Ordinal);
            Flatten(document.RootElement, string.Empty, texts);
            return texts;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _context.Logger.Warning($"Text resource file {file.Name} could not be read ({ex.Message}); keeping the last good texts.");
            return previous;
        }
    }

    private static void Flatten(JsonElement element, string prefix, Dictionary<string, string> texts)
    {
        if (element.ValueKind == JsonValueKind.String && prefix.Length > 0)
        {
            texts[prefix] = element.GetString() ?? string.Empty;
            return;
        }
        if (element.ValueKind != JsonValueKind.Object)
            return;
        foreach (var property in element.EnumerateObject())
            Flatten(property.Value, prefix.Length == 0 ? property.Name : prefix + "." + property.Name, texts);
    }

    private static bool IsCultureName(string? culture) =>
        !string.IsNullOrEmpty(culture) && culture.Length <= 64 && culture.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');

    private sealed record Entry(IReadOnlyDictionary<string, string> Texts, string? FilePath, DateTime LastWriteUtc, long Length, DateTimeOffset CheckedAt);
}

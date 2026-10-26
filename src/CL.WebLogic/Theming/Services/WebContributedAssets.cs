using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace CL.WebLogic.Theming.Services;

/// <summary>
/// Static files shipped by contributors and plugins (<c>b.MapAssets(…)</c>), served from memory
/// under <see cref="UrlPrefix"/><c>{name}/{path}</c>. Entries are removed with their contribution.
/// </summary>
public sealed partial class WebContributedAssets
{
    /// <summary>URL prefix of contributed assets.</summary>
    public const string UrlPrefix = "/_weblogic/plugins/";

    private readonly ConcurrentDictionary<string, (object Owner, IReadOnlyDictionary<string, WebContributedAsset> Files)> _sets = new(StringComparer.Ordinal);
    private readonly Lock _lock = new();

    /// <summary>Raised with the asset set name whenever one is added or removed.</summary>
    public event Action<string>? Changed;

    /// <summary>Names currently registered.</summary>
    public IReadOnlyCollection<string> Names => _sets.Keys.ToList();

    /// <summary>True when <paramref name="name"/> is a valid asset set name (lower-case letters, digits, <c>.-_</c>).</summary>
    public static bool IsValidName(string? name) => name is not null && NamePattern().IsMatch(name);

    /// <summary>The file at <paramref name="path"/> of asset set <paramref name="name"/>.</summary>
    public bool TryGet(string name, string path, out WebContributedAsset asset)
    {
        if (_sets.TryGetValue(name, out var set) && set.Files.TryGetValue(path, out asset!))
            return true;
        asset = null!;
        return false;
    }

    /// <summary>Registers <paramref name="files"/> as asset set <paramref name="name"/> for <paramref name="owner"/>.</summary>
    public void Add(object owner, string name, IReadOnlyDictionary<string, WebContributedAsset> files)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(files);
        if (!IsValidName(name))
            throw new ArgumentException($"'{name}' is not a valid asset name (lower-case letters, digits, '.', '-', '_').", nameof(name));
        lock (_lock)
        {
            if (_sets.TryGetValue(name, out var existing) && !ReferenceEquals(existing.Owner, owner))
                throw new InvalidOperationException($"The asset name '{name}' is already used by another contributor.");
            _sets[name] = (owner, files);
        }
        Changed?.Invoke(name);
    }

    /// <summary>Removes every asset set registered by <paramref name="owner"/>.</summary>
    public void Remove(object owner)
    {
        List<string> removed;
        lock (_lock)
        {
            removed = [.. _sets.Where(s => ReferenceEquals(s.Value.Owner, owner)).Select(s => s.Key)];
            foreach (var name in removed)
                _sets.TryRemove(name, out _);
        }
        foreach (var name in removed)
            Changed?.Invoke(name);
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9._-]{0,63}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex NamePattern();
}

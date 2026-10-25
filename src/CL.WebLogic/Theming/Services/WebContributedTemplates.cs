using System.Collections.Concurrent;
using CL.WebLogic.Templates.Parsing;

namespace CL.WebLogic.Theming.Services;

/// <summary>
/// Templates shipped by contributors and plugins (<c>b.MapTemplates(…)</c>). The active theme is
/// always searched first, so a site can override any contributed template by placing a file at
/// the same path. Entries are removed with their contribution.
/// </summary>
public sealed class WebContributedTemplates
{
    private readonly ConcurrentDictionary<string, Entry> _templates = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _lock = new();

    /// <summary>Raised with the path whenever a template is added or removed.</summary>
    public event Action<string>? Changed;

    /// <summary>Paths currently contributed.</summary>
    public IReadOnlyCollection<string> Paths => _templates.Keys.ToList();

    /// <summary>The source and hash for <paramref name="normalizedPath"/>, when contributed.</summary>
    public bool TryGet(string normalizedPath, out string source, out string hash)
    {
        if (_templates.TryGetValue(normalizedPath, out var entry))
        {
            (source, hash) = (entry.Source, entry.Hash);
            return true;
        }
        (source, hash) = (string.Empty, string.Empty);
        return false;
    }

    /// <summary>
    /// Adds every template for <paramref name="owner"/> or none: a path another owner already
    /// contributed fails the whole call.
    /// </summary>
    public void Add(object owner, IReadOnlyDictionary<string, string> templates)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(templates);
        lock (_lock)
        {
            var taken = templates.Keys.FirstOrDefault(p => _templates.TryGetValue(p, out var e) && !ReferenceEquals(e.Owner, owner));
            if (taken is not null)
                throw new InvalidOperationException($"The template '{taken}' is already contributed by another contributor.");
            foreach (var (path, source) in templates)
                _templates[path] = new Entry(owner, source, TemplateSourceHash.Compute(source));
        }
        foreach (var path in templates.Keys)
            Changed?.Invoke(path);
    }

    /// <summary>Removes every template contributed by <paramref name="owner"/>.</summary>
    public void Remove(object owner)
    {
        List<string> removed;
        lock (_lock)
        {
            removed = _templates.Where(x => ReferenceEquals(x.Value.Owner, owner)).Select(x => x.Key).ToList();
            foreach (var path in removed)
                _templates.TryRemove(path, out _);
        }
        foreach (var path in removed)
            Changed?.Invoke(path);
    }

    private sealed record Entry(object Owner, string Source, string Hash);
}

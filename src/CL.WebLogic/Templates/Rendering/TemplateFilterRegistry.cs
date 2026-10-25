namespace CL.WebLogic.Templates.Rendering;

/// <summary>Built-in and app-registered filters by name.</summary>
public sealed class TemplateFilterRegistry
{
    private readonly Dictionary<string, Func<object?, string?, TemplateRenderContext, object?>> _filters =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Creates the registry with the built-in filters plus <paramref name="custom"/> (custom wins).</summary>
    public TemplateFilterRegistry(IEnumerable<ITemplateFilter> custom)
    {
        TextTemplateFilters.Register(_filters);
        NumberTemplateFilters.Register(_filters);
        MarkupTemplateFilters.Register(_filters);

        foreach (var filter in custom ?? [])
            _filters[filter.Name] = filter.Apply;
    }

    /// <summary>All filter names.</summary>
    public IReadOnlyCollection<string> Names => _filters.Keys;

    /// <summary>
    /// Applies the named filter. An unknown name passes the value through unchanged (as in v1),
    /// so a typo never takes a page down; encoding still applies afterwards.
    /// </summary>
    public object? Apply(string name, object? value, string? argument, TemplateRenderContext context) =>
        _filters.TryGetValue(name, out var filter) ? filter(value, argument, context) : value;

    /// <summary>True when a filter with <paramref name="name"/> exists.</summary>
    public bool Contains(string name) => _filters.ContainsKey(name);
}

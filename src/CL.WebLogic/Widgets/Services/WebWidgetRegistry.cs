using System.Text.RegularExpressions;
using CL.WebLogic.Widgets.Models;

namespace CL.WebLogic.Widgets.Services;

/// <summary>
/// Widgets and widget placements. Lock-free reads from immutable snapshots; writes swap the
/// snapshot under a lock. Entries are removed per reference so a re-registered plugin is safe.
/// </summary>
public sealed partial class WebWidgetRegistry
{
    private readonly Lock _lock = new();
    private Dictionary<string, WebWidgetDefinition> _widgets = new(StringComparer.OrdinalIgnoreCase);
    private WebWidgetPlacement[] _placements = [];

    /// <summary>All widgets.</summary>
    public IReadOnlyCollection<WebWidgetDefinition> Widgets => Volatile.Read(ref _widgets).Values;

    /// <summary>All placements.</summary>
    public IReadOnlyList<WebWidgetPlacement> Placements => Volatile.Read(ref _placements);

    /// <summary>A widget by name.</summary>
    public WebWidgetDefinition? Find(string? name) =>
        name is not null && Volatile.Read(ref _widgets).TryGetValue(name, out var widget) ? widget : null;

    /// <summary>Placements of an area, ordered.</summary>
    public IReadOnlyList<WebWidgetPlacement> PlacementsFor(string area) =>
        [.. Volatile.Read(ref _placements).Where(p => string.Equals(p.Area, area, StringComparison.OrdinalIgnoreCase)).OrderBy(p => p.Order)];

    /// <summary>Adds a widget; a name used by another registration is an error.</summary>
    public void Add(WebWidgetDefinition widget)
    {
        ArgumentNullException.ThrowIfNull(widget);
        if (!IsValidName(widget.Name))
            throw new ArgumentException($"Widget name '{widget.Name}' must match ^[a-z0-9][a-z0-9._-]{{0,63}}$.", nameof(widget));
        lock (_lock)
        {
            if (_widgets.TryGetValue(widget.Name, out var existing))
                throw new InvalidOperationException($"Widget '{widget.Name}' is already registered by '{existing.Contributor.Id}'.");
            Volatile.Write(ref _widgets, new Dictionary<string, WebWidgetDefinition>(_widgets, StringComparer.OrdinalIgnoreCase) { [widget.Name] = widget });
        }
    }

    /// <summary>Removes exactly this widget registration.</summary>
    public bool Remove(WebWidgetDefinition widget)
    {
        lock (_lock)
        {
            if (!_widgets.TryGetValue(widget.Name, out var existing) || !ReferenceEquals(existing, widget))
                return false;
            var next = new Dictionary<string, WebWidgetDefinition>(_widgets, StringComparer.OrdinalIgnoreCase);
            next.Remove(widget.Name);
            Volatile.Write(ref _widgets, next);
            return true;
        }
    }

    /// <summary>Adds a placement.</summary>
    public void AddPlacement(WebWidgetPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);
        lock (_lock)
            Volatile.Write(ref _placements, [.. _placements, placement]);
    }

    /// <summary>Removes exactly this placement.</summary>
    public bool RemovePlacement(WebWidgetPlacement placement)
    {
        lock (_lock)
        {
            var next = _placements.Where(p => !ReferenceEquals(p, placement)).ToArray();
            var removed = next.Length != _placements.Length;
            Volatile.Write(ref _placements, next);
            return removed;
        }
    }

    /// <summary>True for a valid widget name.</summary>
    public static bool IsValidName(string? name) => name is not null && WidgetName().IsMatch(name);

    /// <summary>True for a valid instance id.</summary>
    public static bool IsValidInstanceId(string? id) => id is not null && InstanceId().IsMatch(id);

    [GeneratedRegex("^[a-z0-9][a-z0-9._-]{0,63}$", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex WidgetName();

    [GeneratedRegex("^[A-Za-z0-9_.:-]{1,128}$", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex InstanceId();
}

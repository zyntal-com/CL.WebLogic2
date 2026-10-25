using System.Collections.Immutable;
using System.Text.RegularExpressions;
using CL.WebLogic.Routing.Models;
using Microsoft.Extensions.Primitives;

namespace CL.WebLogic.Routing.Services;

/// <summary>
/// Thread-safe registry of committed routes, grouped by contributor so a contributor's routes
/// can be removed as one unit (plugin unload). Every change swaps an immutable snapshot and
/// fires the change token the endpoint data source listens to.
/// </summary>
public sealed partial class WebRouteRegistry : IDisposable
{
    private readonly object _lock = new();
    private ImmutableDictionary<string, ImmutableArray<WebRouteDefinition>> _byContributor =
        ImmutableDictionary.Create<string, ImmutableArray<WebRouteDefinition>>(StringComparer.Ordinal);
    private ImmutableArray<WebRouteDefinition> _routes = [];
    private CancellationTokenSource _changeSource = new();
    private long _version;

    /// <summary>Incremented on every change; lets consumers detect a stale snapshot cheaply.</summary>
    public long Version => Interlocked.Read(ref _version);

    /// <summary>Snapshot of every committed route.</summary>
    public IReadOnlyList<WebRouteDefinition> Routes => _routes;

    /// <summary>Ids of the contributors that currently own routes.</summary>
    public IReadOnlyCollection<string> ContributorIds => _byContributor.Keys.ToList();

    /// <summary>A token that fires on the next change to the registry.</summary>
    public IChangeToken GetChangeToken() => new CancellationChangeToken(Volatile.Read(ref _changeSource).Token);

    /// <summary>Routes owned by <paramref name="contributorId"/>.</summary>
    public IReadOnlyList<WebRouteDefinition> GetRoutes(string contributorId) =>
        _byContributor.TryGetValue(contributorId, out var routes) ? routes : [];

    /// <summary>
    /// Adds routes for a contributor that owns none yet.
    /// Throws when a route would be ambiguous with an existing one (same pattern and an
    /// overlapping method) or when a second fallback is registered.
    /// </summary>
    public void Add(string contributorId, IReadOnlyList<WebRouteDefinition> routes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contributorId);
        ArgumentNullException.ThrowIfNull(routes);

        lock (_lock)
        {
            if (_byContributor.ContainsKey(contributorId))
                throw new InvalidOperationException($"Contributor '{contributorId}' already has routes registered.");

            var existing = _routes;
            var accepted = new List<WebRouteDefinition>(routes.Count);
            foreach (var route in routes)
            {
                EnsureNoConflict(route, existing.Concat(accepted));
                accepted.Add(route);
            }

            _byContributor = _byContributor.SetItem(contributorId, [.. accepted]);
            _routes = _routes.AddRange(accepted);
            SignalChange();
        }
    }

    /// <summary>Removes every route owned by <paramref name="contributorId"/>.</summary>
    /// <returns>True when the contributor owned routes.</returns>
    public bool Remove(string contributorId)
    {
        lock (_lock)
        {
            if (!_byContributor.TryGetValue(contributorId, out var owned))
                return false;

            var ids = owned.Select(r => r.Id).ToHashSet();
            _byContributor = _byContributor.Remove(contributorId);
            _routes = _routes.RemoveAll(r => ids.Contains(r.Id));
            SignalChange();
            return true;
        }
    }

    /// <inheritdoc/>
    public void Dispose() => _changeSource.Dispose();

    private void SignalChange()
    {
        Interlocked.Increment(ref _version);
        var previous = Interlocked.Exchange(ref _changeSource, new CancellationTokenSource());
        previous.Cancel();
        previous.Dispose();
    }

    private static void EnsureNoConflict(WebRouteDefinition route, IEnumerable<WebRouteDefinition> others)
    {
        var meta = route.Metadata;
        var shape = Shape(meta.Pattern);

        foreach (var other in others)
        {
            if (meta.Kind == WebRouteKind.Fallback && other.Metadata.Kind == WebRouteKind.Fallback)
                throw new InvalidOperationException(
                    $"Only one fallback route is allowed; '{other.Metadata.Contributor.Id}' already registered one.");

            if (!string.Equals(shape, Shape(other.Metadata.Pattern), StringComparison.Ordinal))
                continue;

            var overlap = meta.Methods.Intersect(other.Metadata.Methods, StringComparer.Ordinal).ToList();
            if (overlap.Count > 0)
                throw new InvalidOperationException(
                    $"Route '{string.Join(",", overlap)} {meta.Pattern}' from '{meta.Contributor.Id}' conflicts with " +
                    $"'{other.Metadata.Pattern}' from '{other.Metadata.Contributor.Id}'.");
        }
    }

    /// <summary>Pattern shape for ambiguity checks: case-insensitive, parameter names dropped, constraints kept.</summary>
    private static string Shape(string pattern) =>
        ParameterName().Replace(pattern.ToLowerInvariant().TrimEnd('/'), m => "{" + m.Groups["rest"].Value + "}");

    [GeneratedRegex(@"\{\**[^}:=?]+(?<rest>[^}]*)\}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ParameterName();
}

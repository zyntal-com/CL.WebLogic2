using System.Collections.Concurrent;
using CL.WebLogic.Security.Services;

namespace CL.WebLogic.Security.Sessions;

/// <summary>Development session store: process memory, lost on restart, not shared between nodes.</summary>
public sealed class InMemoryWebSessionStore : IWebSessionStore, IWebSessionListStore, IWebDevelopmentStore
{
    private readonly ConcurrentDictionary<string, WebSessionRecord> _sessions = new(StringComparer.Ordinal);

    /// <summary>Stored sessions.</summary>
    public int Count => _sessions.Count;

    /// <inheritdoc/>
    public Task<WebSessionRecord?> GetAsync(string idHash, CancellationToken cancellationToken) =>
        Task.FromResult(_sessions.TryGetValue(idHash, out var record) ? record : null);

    /// <inheritdoc/>
    public Task SaveAsync(WebSessionRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        _sessions[record.IdHash] = record;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task DeleteAsync(string idHash, CancellationToken cancellationToken)
    {
        _sessions.TryRemove(idHash, out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<int> DeleteForUserAsync(string userId, CancellationToken cancellationToken) =>
        Task.FromResult(RemoveWhere(r => r.UserId == userId));

    /// <inheritdoc/>
    public Task<IReadOnlyList<WebSessionRecord>> ListForUserAsync(string userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<WebSessionRecord>>([.. _sessions.Values.Where(r => r.UserId == userId)]);

    /// <inheritdoc/>
    public Task<int> DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        Task.FromResult(RemoveWhere(r => r.ExpiresAt < now));

    private int RemoveWhere(Func<WebSessionRecord, bool> predicate)
    {
        var removed = 0;
        foreach (var (key, record) in _sessions)
        {
            if (predicate(record) && _sessions.TryRemove(key, out _))
                removed++;
        }
        return removed;
    }
}

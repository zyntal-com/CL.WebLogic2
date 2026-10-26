using CL.WebLogic.Security.Sessions;

namespace StarterWebsite.Infrastructure;

/// <summary>Sessions in <c>sessions.json</c>, keyed by the hash of the session id (the toolkit never hands over raw ids).</summary>
public sealed class JsonSessionStore : IWebSessionStore, IWebSessionListStore, IDisposable
{
    private readonly JsonFileStore<WebSessionRecord> _sessions;

    /// <summary>Stores in <paramref name="folder"/>.</summary>
    public JsonSessionStore(StarterDataFolder folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        _sessions = new JsonFileStore<WebSessionRecord>(folder.File("sessions.json"));
    }

    /// <inheritdoc/>
    public Task<WebSessionRecord?> GetAsync(string idHash, CancellationToken cancellationToken) => _sessions.GetAsync(idHash, cancellationToken);

    /// <inheritdoc/>
    public Task SaveAsync(WebSessionRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        return _sessions.SetAsync(record.IdHash, record, cancellationToken);
    }

    /// <inheritdoc/>
    public Task DeleteAsync(string idHash, CancellationToken cancellationToken) =>
        _sessions.RemoveWhereAsync((key, _) => key == idHash, cancellationToken);

    /// <inheritdoc/>
    public Task<int> DeleteForUserAsync(string userId, CancellationToken cancellationToken) =>
        _sessions.RemoveWhereAsync((_, record) => record.UserId == userId, cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WebSessionRecord>> ListForUserAsync(string userId, CancellationToken cancellationToken) =>
        await _sessions.WhereAsync(record => record.UserId == userId, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public Task<int> DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        _sessions.RemoveWhereAsync((_, record) => record.ExpiresAt < now, cancellationToken);

    /// <inheritdoc/>
    public void Dispose() => _sessions.Dispose();
}

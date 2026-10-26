using CL.WebLogic.MySql.Entities;
using CL.WebLogic.MySql.Services;
using CL.WebLogic.Security.Sessions;

namespace CL.WebLogic.MySql.Stores;

/// <summary>
/// Sessions in <c>wl_sessions</c>, keyed by the SHA-256 of the session id (raw ids are never
/// stored). Supports session listing (signed-in devices, <c>MaxPerUser</c>, new-device mail).
/// </summary>
public sealed class MySqlWebSessionStore : IWebSessionStore, IWebSessionListStore
{
    private readonly WebMySqlDatabase _db;

    /// <summary>Creates the store.</summary>
    public MySqlWebSessionStore(WebMySqlDatabase db) => _db = db ?? throw new ArgumentNullException(nameof(db));

    /// <inheritdoc/>
    public async Task<WebSessionRecord?> GetAsync(string idHash, CancellationToken cancellationToken)
    {
        var row = WebMySqlDatabase.Value(await _db.Repository<SessionRow>().GetByIdAsync(idHash, cancellationToken).ConfigureAwait(false));
        return row is null ? null : WebMySqlJson.Read<WebSessionRecord>(row.RecordJson);
    }

    /// <inheritdoc/>
    public async Task SaveAsync(WebSessionRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        WebMySqlDatabase.Value(await _db.Repository<SessionRow>().UpsertAsync(new SessionRow
        {
            IdHash = record.IdHash,
            UserId = record.UserId,
            ExpiresUtc = record.ExpiresAt.UtcDateTime,
            RecordJson = WebMySqlJson.Write(record)
        }, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string idHash, CancellationToken cancellationToken) =>
        WebMySqlDatabase.Value(await _db.Repository<SessionRow>().DeleteAsync(idHash, cancellationToken).ConfigureAwait(false));

    /// <inheritdoc/>
    public async Task<int> DeleteForUserAsync(string userId, CancellationToken cancellationToken) =>
        WebMySqlDatabase.Value(await _db.Query<SessionRow>().Where(s => s.UserId == userId).DeleteAsync(cancellationToken).ConfigureAwait(false));

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WebSessionRecord>> ListForUserAsync(string userId, CancellationToken cancellationToken)
    {
        var rows = WebMySqlDatabase.Value(await _db.Query<SessionRow>().Where(s => s.UserId == userId).ToListAsync(cancellationToken).ConfigureAwait(false));
        return [.. rows.Select(r => WebMySqlJson.Read<WebSessionRecord>(r.RecordJson))];
    }

    /// <inheritdoc/>
    public async Task<int> DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var cutoff = now.UtcDateTime;
        return WebMySqlDatabase.Value(await _db.Query<SessionRow>().Where(s => s.ExpiresUtc < cutoff).DeleteAsync(cancellationToken).ConfigureAwait(false));
    }
}

using CL.WebLogic.Data.Entities;
using CL.WebLogic.Data.Services;
using CL.WebLogic.Security.Sessions;

namespace CL.WebLogic.Data.Stores;

/// <summary>
/// Sessions in <c>wl_sessions</c>, keyed by the SHA-256 of the session id (raw ids are never
/// stored). Supports session listing (signed-in devices, <c>MaxPerUser</c>, new-device mail).
/// </summary>
public sealed class DataWebSessionStore : IWebSessionStore, IWebSessionListStore
{
    private readonly WebDataDatabase _db;

    /// <summary>Creates the store.</summary>
    public DataWebSessionStore(WebDataDatabase db) => _db = db ?? throw new ArgumentNullException(nameof(db));

    /// <inheritdoc/>
    public async Task<WebSessionRecord?> GetAsync(string idHash, CancellationToken cancellationToken)
    {
        var row = WebDataDatabase.Value(await _db.Query<SessionRow>().Where(r => r.IdHash == idHash).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false));
        return row is null ? null : WebDataJson.Read<WebSessionRecord>(row.RecordJson);
    }

    /// <inheritdoc/>
    public async Task SaveAsync(WebSessionRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        WebDataDatabase.Value(await _db.Repository<SessionRow>().UpsertAsync(new SessionRow
        {
            IdHash = record.IdHash,
            UserId = record.UserId,
            ExpiresUtc = record.ExpiresAt.UtcDateTime,
            RecordJson = WebDataJson.Write(record)
        }, ct: cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string idHash, CancellationToken cancellationToken) =>
        WebDataDatabase.Value(await _db.Repository<SessionRow>().DeleteByIdAsync(idHash, cancellationToken).ConfigureAwait(false));

    /// <inheritdoc/>
    public async Task<int> DeleteForUserAsync(string userId, CancellationToken cancellationToken) =>
        WebDataDatabase.Value(await _db.Query<SessionRow>().Where(s => s.UserId == userId).DeleteAsync(ct: cancellationToken).ConfigureAwait(false));

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WebSessionRecord>> ListForUserAsync(string userId, CancellationToken cancellationToken)
    {
        var rows = WebDataDatabase.Value(await _db.Query<SessionRow>().Where(s => s.UserId == userId).ToListAsync(cancellationToken).ConfigureAwait(false));
        return [.. rows.Select(r => WebDataJson.Read<WebSessionRecord>(r.RecordJson))];
    }

    /// <inheritdoc/>
    public async Task<int> DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var cutoff = now.UtcDateTime;
        return WebDataDatabase.Value(await _db.Query<SessionRow>().Where(s => s.ExpiresUtc < cutoff).DeleteAsync(ct: cancellationToken).ConfigureAwait(false));
    }
}

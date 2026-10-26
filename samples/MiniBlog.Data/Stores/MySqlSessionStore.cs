using System.Text.Json;
using CL.WebLogic.Security.Sessions;
using MiniBlog.Data.Entities;

namespace MiniBlog.Data.Stores;

/// <summary>Sessions in <c>miniblog_sessions</c>; shared by every instance behind a load balancer.</summary>
public sealed class MySqlSessionStore : IWebSessionStore, IWebSessionListStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly BlogDatabase _db;

    /// <summary>Creates the store.</summary>
    public MySqlSessionStore(BlogDatabase db) => _db = db;

    /// <inheritdoc/>
    public async Task<WebSessionRecord?> GetAsync(string idHash, CancellationToken cancellationToken)
    {
        var row = await _db.MySql.GetRepository<SessionEntity>(_db.ConnectionId).GetByIdAsync(idHash, cancellationToken).ConfigureAwait(false);
        if (row.IsFailure)
            throw new InvalidOperationException(row.Error!.Message);
        return row.Value is null ? null : JsonSerializer.Deserialize<WebSessionRecord>(row.Value.RecordJson, Json);
    }

    /// <inheritdoc/>
    public async Task SaveAsync(WebSessionRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        var entity = new SessionEntity
        {
            IdHash = record.IdHash,
            UserId = record.UserId,
            ExpiresUtc = record.ExpiresAt.UtcDateTime,
            RecordJson = JsonSerializer.Serialize(record, Json)
        };
        var saved = await _db.MySql.GetRepository<SessionEntity>(_db.ConnectionId).UpsertAsync(entity, cancellationToken).ConfigureAwait(false);
        if (saved.IsFailure)
            throw new InvalidOperationException(saved.Error!.Message);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string idHash, CancellationToken cancellationToken) =>
        await _db.MySql.GetRepository<SessionEntity>(_db.ConnectionId).DeleteAsync(idHash, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task<int> DeleteForUserAsync(string userId, CancellationToken cancellationToken)
    {
        var deleted = await _db.MySql.Query<SessionEntity>(_db.ConnectionId).Where(s => s.UserId == userId).DeleteAsync(cancellationToken).ConfigureAwait(false);
        return deleted.IsSuccess ? deleted.Value : 0;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WebSessionRecord>> ListForUserAsync(string userId, CancellationToken cancellationToken)
    {
        var rows = await _db.MySql.Query<SessionEntity>(_db.ConnectionId).Where(s => s.UserId == userId).ToListAsync(cancellationToken).ConfigureAwait(false);
        if (rows.IsFailure)
            throw new InvalidOperationException(rows.Error!.Message);
        return [.. rows.Value!.Select(r => JsonSerializer.Deserialize<WebSessionRecord>(r.RecordJson, Json)!)];
    }

    /// <inheritdoc/>
    public async Task<int> DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var cutoff = now.UtcDateTime;
        var deleted = await _db.MySql.Query<SessionEntity>(_db.ConnectionId).Where(s => s.ExpiresUtc < cutoff).DeleteAsync(cancellationToken).ConfigureAwait(false);
        return deleted.IsSuccess ? deleted.Value : 0;
    }
}

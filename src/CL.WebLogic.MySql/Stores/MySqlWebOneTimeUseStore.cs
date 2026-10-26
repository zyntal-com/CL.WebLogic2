using System.Security.Cryptography;
using System.Text;
using CL.MySQL2.Models;
using CL.WebLogic.Auth.Tokens;
using CL.WebLogic.MySql.Entities;
using CL.WebLogic.MySql.Services;

namespace CL.WebLogic.MySql.Stores;

/// <summary>
/// Spent single-use keys in <c>wl_one_time_use</c> (TOTP steps, link tokens), stored as SHA-256.
/// The insert is <c>INSERT IGNORE</c> on the primary key, so exactly one request wins a key across
/// every node; an expired key may be used again, as with the in-memory store.
/// </summary>
public sealed class MySqlWebOneTimeUseStore : IWebOneTimeUseStore
{
    private readonly WebMySqlDatabase _db;
    private readonly TimeProvider _time;

    /// <summary>Creates the store.</summary>
    public MySqlWebOneTimeUseStore(WebMySqlDatabase db, TimeProvider? time = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _time = time ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> TryUseAsync(string key, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        var now = _time.GetUtcNow();
        if (expiresAt <= now)
            return false;

        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
        var row = new OneTimeUseRow { UseKey = hash, ExpiresUtc = expiresAt.UtcDateTime };
        var repository = _db.Repository<OneTimeUseRow>();
        // The insert decides the winner: one round trip for a fresh key.
        if (WebMySqlDatabase.Value(await repository.InsertIgnoreAsync(row, cancellationToken).ConfigureAwait(false)) == InsertOutcome.Inserted)
            return true;

        // Taken: free the key only if that earlier use has expired, then compete again.
        var nowUtc = now.UtcDateTime;
        var freed = WebMySqlDatabase.Value(await _db.Query<OneTimeUseRow>().Where(u => u.UseKey == hash && u.ExpiresUtc <= nowUtc).DeleteAsync(cancellationToken).ConfigureAwait(false));
        return freed > 0 && WebMySqlDatabase.Value(await repository.InsertIgnoreAsync(row, cancellationToken).ConfigureAwait(false)) == InsertOutcome.Inserted;
    }

    /// <summary>Deletes expired keys (called by the sweep).</summary>
    public async Task<int> DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var nowUtc = now.UtcDateTime;
        return WebMySqlDatabase.Value(await _db.Query<OneTimeUseRow>().Where(u => u.ExpiresUtc <= nowUtc).DeleteAsync(cancellationToken).ConfigureAwait(false));
    }
}

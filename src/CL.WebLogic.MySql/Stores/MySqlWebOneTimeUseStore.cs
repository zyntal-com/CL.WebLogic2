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
        // Free the key if its earlier use has expired; the insert below then decides the winner.
        await _db.ExecuteAsync("DELETE FROM wl_one_time_use WHERE use_key = @key AND expires_utc <= @now",
            new Dictionary<string, object?> { ["@key"] = hash, ["@now"] = now.UtcDateTime }, cancellationToken).ConfigureAwait(false);
        var outcome = WebMySqlDatabase.Value(await _db.Repository<OneTimeUseRow>().InsertIgnoreAsync(
            new OneTimeUseRow { UseKey = hash, ExpiresUtc = expiresAt.UtcDateTime }, cancellationToken).ConfigureAwait(false));
        return outcome == InsertOutcome.Inserted;
    }

    /// <summary>Deletes expired keys (called by the sweep).</summary>
    public Task<int> DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        _db.ExecuteAsync("DELETE FROM wl_one_time_use WHERE expires_utc <= @now",
            new Dictionary<string, object?> { ["@now"] = now.UtcDateTime }, cancellationToken);
}

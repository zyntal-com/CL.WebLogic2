using CL.WebLogic.Auth.TwoFactor;
using CL.WebLogic.MySql.Entities;
using CL.WebLogic.MySql.Services;

namespace CL.WebLogic.MySql.Stores;

/// <summary>
/// Hashed recovery codes in <c>wl_recovery_codes</c>, one row per unused code. Consuming a code is
/// a single <c>DELETE</c>, so two requests can never both use the same code.
/// </summary>
public sealed class MySqlWebRecoveryCodeStore : IWebRecoveryCodeStore
{
    private readonly WebMySqlDatabase _db;

    /// <summary>Creates the store.</summary>
    public MySqlWebRecoveryCodeStore(WebMySqlDatabase db) => _db = db ?? throw new ArgumentNullException(nameof(db));

    /// <inheritdoc/>
    public async Task ReplaceAsync(string userId, IReadOnlyList<string> codeHashes, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        ArgumentNullException.ThrowIfNull(codeHashes);
        WebMySqlDatabase.Value(await _db.Query<RecoveryCodeRow>().Where(c => c.UserId == userId).DeleteAsync(cancellationToken).ConfigureAwait(false));
        if (codeHashes.Count > 0)
        {
            WebMySqlDatabase.Value(await _db.Repository<RecoveryCodeRow>().InsertManyIgnoreAsync(
                codeHashes.Distinct(StringComparer.Ordinal).Select(h => new RecoveryCodeRow { CodeKey = Key(userId, h), UserId = userId }),
                cancellationToken).ConfigureAwait(false));
        }
    }

    /// <inheritdoc/>
    public async Task<bool> TryConsumeAsync(string userId, string codeHash, CancellationToken cancellationToken) =>
        await _db.ExecuteAsync("DELETE FROM wl_recovery_codes WHERE code_key = @key",
            new Dictionary<string, object?> { ["@key"] = Key(userId, codeHash) }, cancellationToken).ConfigureAwait(false) == 1;

    /// <inheritdoc/>
    public async Task<int> CountAsync(string userId, CancellationToken cancellationToken) =>
        (int)WebMySqlDatabase.Value(await _db.Query<RecoveryCodeRow>().Where(c => c.UserId == userId).CountAsync(cancellationToken).ConfigureAwait(false));

    private static string Key(string userId, string codeHash) => userId + ":" + codeHash;
}

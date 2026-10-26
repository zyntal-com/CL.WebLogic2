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
        // One transaction: the user never ends up with no codes, or old and new codes mixed.
        await using var transaction = await _db.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        WebMySqlDatabase.Value(await _db.Query<RecoveryCodeRow>(transaction).Where(c => c.UserId == userId).DeleteAsync(cancellationToken).ConfigureAwait(false));
        if (codeHashes.Count > 0)
        {
            WebMySqlDatabase.Value(await _db.Repository<RecoveryCodeRow>(transaction).InsertManyIgnoreAsync(
                codeHashes.Distinct(StringComparer.Ordinal).Select(h => new RecoveryCodeRow { CodeKey = Key(userId, h), UserId = userId }),
                cancellationToken).ConfigureAwait(false));
        }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<bool> TryConsumeAsync(string userId, string codeHash, CancellationToken cancellationToken)
    {
        var key = Key(userId, codeHash);
        return WebMySqlDatabase.Value(await _db.Query<RecoveryCodeRow>().Where(c => c.CodeKey == key).DeleteAsync(cancellationToken).ConfigureAwait(false)) == 1;
    }

    /// <inheritdoc/>
    public async Task<int> CountAsync(string userId, CancellationToken cancellationToken) =>
        (int)WebMySqlDatabase.Value(await _db.Query<RecoveryCodeRow>().Where(c => c.UserId == userId).CountAsync(cancellationToken).ConfigureAwait(false));

    private static string Key(string userId, string codeHash) => userId + ":" + codeHash;
}

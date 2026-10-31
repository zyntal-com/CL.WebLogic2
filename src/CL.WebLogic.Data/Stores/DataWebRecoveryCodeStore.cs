using CL.WebLogic.Auth.TwoFactor;
using CL.WebLogic.Data.Entities;
using CL.WebLogic.Data.Services;

namespace CL.WebLogic.Data.Stores;

/// <summary>
/// Hashed recovery codes in <c>wl_recovery_codes</c>, one row per unused code. Consuming a code is
/// a single <c>DELETE</c>, so two requests can never both use the same code.
/// </summary>
public sealed class DataWebRecoveryCodeStore : IWebRecoveryCodeStore
{
    private readonly WebDataDatabase _db;

    /// <summary>Creates the store.</summary>
    public DataWebRecoveryCodeStore(WebDataDatabase db) => _db = db ?? throw new ArgumentNullException(nameof(db));

    /// <inheritdoc/>
    public async Task ReplaceAsync(string userId, IReadOnlyList<string> codeHashes, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        ArgumentNullException.ThrowIfNull(codeHashes);
        // One transaction: the user never ends up with no codes, or old and new codes mixed.
        var rows = codeHashes.Distinct(StringComparer.Ordinal).Select(h => new RecoveryCodeRow { CodeKey = Key(userId, h), UserId = userId }).ToList();
        await _db.InTransactionAsync(async (transaction, ct) =>
        {
            WebDataDatabase.Value(await transaction.Query<RecoveryCodeRow>().Where(c => c.UserId == userId).DeleteAsync(ct: ct).ConfigureAwait(false));
            if (rows.Count > 0)
                WebDataDatabase.Value(await transaction.Repository<RecoveryCodeRow>().InsertManyAsync(rows, ct).ConfigureAwait(false));
            return true;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<bool> TryConsumeAsync(string userId, string codeHash, CancellationToken cancellationToken)
    {
        var key = Key(userId, codeHash);
        return WebDataDatabase.Value(await _db.Query<RecoveryCodeRow>().Where(c => c.CodeKey == key).DeleteAsync(ct: cancellationToken).ConfigureAwait(false)) == 1;
    }

    /// <inheritdoc/>
    public async Task<int> CountAsync(string userId, CancellationToken cancellationToken) =>
        (int)WebDataDatabase.Value(await _db.Query<RecoveryCodeRow>().Where(c => c.UserId == userId).CountAsync(cancellationToken).ConfigureAwait(false));

    private static string Key(string userId, string codeHash) => userId + ":" + codeHash;
}

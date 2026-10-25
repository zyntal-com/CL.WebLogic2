using CL.WebLogic.Auth.TwoFactor;

namespace MiniBlog.Data.Stores;

/// <summary>Hashed recovery codes as documents; consuming is a versioned write, so a code works once.</summary>
public sealed class MySqlRecoveryCodeStore : IWebRecoveryCodeStore
{
    private const string Collection = "recovery";
    private readonly DocumentStore _documents;

    /// <summary>Creates the store.</summary>
    public MySqlRecoveryCodeStore(DocumentStore documents) => _documents = documents;

    /// <inheritdoc/>
    public Task ReplaceAsync(string userId, IReadOnlyList<string> codeHashes, CancellationToken cancellationToken) =>
        _documents.SetAsync(Collection, userId, codeHashes.ToList(), cancellationToken);

    /// <inheritdoc/>
    public async Task<bool> TryConsumeAsync(string userId, string codeHash, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var (codes, version) = await _documents.GetAsync<List<string>>(Collection, userId, cancellationToken).ConfigureAwait(false);
            if (codes is null || !codes.Remove(codeHash))
                return false;
            if (await _documents.TrySetAsync(Collection, userId, codes, version, cancellationToken).ConfigureAwait(false) is not null)
                return true;
        }
        return false;
    }

    /// <inheritdoc/>
    public async Task<int> CountAsync(string userId, CancellationToken cancellationToken) =>
        (await _documents.GetAsync<List<string>>(Collection, userId, cancellationToken).ConfigureAwait(false)).Value?.Count ?? 0;
}

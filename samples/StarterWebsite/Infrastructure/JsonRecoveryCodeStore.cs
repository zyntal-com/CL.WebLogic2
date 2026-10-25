using CL.WebLogic.Auth.TwoFactor;

namespace StarterWebsite.Infrastructure;

/// <summary>Hashed two-factor recovery codes in <c>recovery-codes.json</c>; consuming one is atomic.</summary>
public sealed class JsonRecoveryCodeStore : IWebRecoveryCodeStore, IDisposable
{
    private readonly JsonFileStore<List<string>> _codes;

    /// <summary>Stores in <paramref name="folder"/>.</summary>
    public JsonRecoveryCodeStore(StarterDataFolder folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        _codes = new JsonFileStore<List<string>>(folder.File("recovery-codes.json"));
    }

    /// <inheritdoc/>
    public Task ReplaceAsync(string userId, IReadOnlyList<string> codeHashes, CancellationToken cancellationToken) =>
        _codes.SetAsync(userId, [.. codeHashes], cancellationToken);

    /// <inheritdoc/>
    public Task<bool> TryConsumeAsync(string userId, string codeHash, CancellationToken cancellationToken) =>
        _codes.UpdateAsync(items =>
        {
            var removed = items.TryGetValue(userId, out var hashes) && hashes.Remove(codeHash);
            return (removed, removed);
        }, cancellationToken);

    /// <inheritdoc/>
    public async Task<int> CountAsync(string userId, CancellationToken cancellationToken) =>
        (await _codes.GetAsync(userId, cancellationToken).ConfigureAwait(false))?.Count ?? 0;

    /// <inheritdoc/>
    public void Dispose() => _codes.Dispose();
}

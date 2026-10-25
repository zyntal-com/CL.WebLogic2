namespace CL.WebLogic.Auth.TwoFactor;

/// <summary>Default when the app registered no recovery code store: two-factor enrollment fails loudly.</summary>
public sealed class NullRecoveryCodeStore : IWebRecoveryCodeStore
{
    /// <inheritdoc/>
    public Task ReplaceAsync(string userId, IReadOnlyList<string> codeHashes, CancellationToken cancellationToken) =>
        codeHashes.Count == 0 ? Task.CompletedTask
            : throw new InvalidOperationException("No recovery code store is registered. Call web.UseRecoveryCodeStore<T>().");

    /// <inheritdoc/>
    public Task<bool> TryConsumeAsync(string userId, string codeHash, CancellationToken cancellationToken) => Task.FromResult(false);

    /// <inheritdoc/>
    public Task<int> CountAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(0);
}

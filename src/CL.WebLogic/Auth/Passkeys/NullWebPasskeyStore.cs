namespace CL.WebLogic.Auth.Passkeys;

/// <summary>Default when the app registered no passkey store: nobody has passkeys and registering one fails loudly.</summary>
public sealed class NullWebPasskeyStore : IWebPasskeyStore
{
    /// <inheritdoc/>
    public Task<IReadOnlyList<WebPasskey>> ListAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<WebPasskey>>([]);

    /// <inheritdoc/>
    public Task<WebPasskey?> FindAsync(string credentialId, CancellationToken cancellationToken) => Task.FromResult<WebPasskey?>(null);

    /// <inheritdoc/>
    public Task<bool> AddAsync(WebPasskey passkey, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("No passkey store is registered. Call web.UsePasskeyStore<T>().");

    /// <inheritdoc/>
    public Task<bool> TryRecordUseAsync(string credentialId, long expectedSignCount, long signCount, bool backedUp, DateTimeOffset usedAt, CancellationToken cancellationToken) =>
        Task.FromResult(false);

    /// <inheritdoc/>
    public Task<bool> RenameAsync(string userId, string credentialId, string name, CancellationToken cancellationToken) => Task.FromResult(false);

    /// <inheritdoc/>
    public Task<bool> DeleteAsync(string userId, string credentialId, CancellationToken cancellationToken) => Task.FromResult(false);
}

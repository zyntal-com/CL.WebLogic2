using CL.WebLogic.Auth.Passkeys;

namespace StarterWebsite.Infrastructure;

/// <summary>Passkeys in <c>passkeys.json</c>, keyed by credential id; the counter update is atomic.</summary>
public sealed class JsonPasskeyStore : IWebPasskeyStore, IDisposable
{
    private readonly JsonFileStore<WebPasskey> _passkeys;

    /// <summary>Stores in <paramref name="folder"/>.</summary>
    public JsonPasskeyStore(StarterDataFolder folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        _passkeys = new JsonFileStore<WebPasskey>(folder.File("passkeys.json"));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WebPasskey>> ListAsync(string userId, CancellationToken cancellationToken) =>
        [.. (await _passkeys.WhereAsync(p => p.UserId == userId, cancellationToken).ConfigureAwait(false)).OrderBy(p => p.CreatedAt)];

    /// <inheritdoc/>
    public Task<WebPasskey?> FindAsync(string credentialId, CancellationToken cancellationToken) =>
        _passkeys.GetAsync(credentialId, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> AddAsync(WebPasskey passkey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(passkey);
        return _passkeys.UpdateAsync(items =>
        {
            var added = items.TryAdd(passkey.CredentialId, passkey);
            return (added, added);
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> TryRecordUseAsync(string credentialId, long expectedSignCount, long signCount, bool backedUp, DateTimeOffset usedAt, CancellationToken cancellationToken) =>
        Change(credentialId, p => p.SignCount == expectedSignCount, p => p with { SignCount = signCount, BackedUp = backedUp, LastUsedAt = usedAt }, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> RenameAsync(string userId, string credentialId, string name, CancellationToken cancellationToken) =>
        Change(credentialId, p => p.UserId == userId, p => p with { Name = name }, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> DeleteAsync(string userId, string credentialId, CancellationToken cancellationToken) =>
        _passkeys.UpdateAsync(items =>
        {
            var removed = items.TryGetValue(credentialId, out var passkey) && passkey.UserId == userId && items.Remove(credentialId);
            return (removed, removed);
        }, cancellationToken);

    /// <inheritdoc/>
    public void Dispose() => _passkeys.Dispose();

    private Task<bool> Change(string credentialId, Func<WebPasskey, bool> condition, Func<WebPasskey, WebPasskey> change, CancellationToken cancellationToken) =>
        _passkeys.UpdateAsync(items =>
        {
            if (!items.TryGetValue(credentialId, out var passkey) || !condition(passkey))
                return (false, false);
            items[credentialId] = change(passkey);
            return (true, true);
        }, cancellationToken);
}

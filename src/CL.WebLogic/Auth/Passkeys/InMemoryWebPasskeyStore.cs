using System.Collections.Concurrent;
using CL.WebLogic.Security.Services;

namespace CL.WebLogic.Auth.Passkeys;

/// <summary>Development passkey store (process memory).</summary>
public sealed class InMemoryWebPasskeyStore : IWebPasskeyStore, IWebDevelopmentStore
{
    private readonly ConcurrentDictionary<string, WebPasskey> _passkeys = new(StringComparer.Ordinal);
    private readonly object _lock = new();

    /// <inheritdoc/>
    public Task<IReadOnlyList<WebPasskey>> ListAsync(string userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<WebPasskey>>([.. _passkeys.Values.Where(p => p.UserId == userId).OrderBy(p => p.CreatedAt)]);

    /// <inheritdoc/>
    public Task<WebPasskey?> FindAsync(string credentialId, CancellationToken cancellationToken) =>
        Task.FromResult(_passkeys.GetValueOrDefault(credentialId));

    /// <inheritdoc/>
    public Task<bool> AddAsync(WebPasskey passkey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(passkey);
        return Task.FromResult(_passkeys.TryAdd(passkey.CredentialId, passkey));
    }

    /// <inheritdoc/>
    public Task<bool> TryRecordUseAsync(string credentialId, long expectedSignCount, long signCount, bool backedUp, DateTimeOffset usedAt, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (!_passkeys.TryGetValue(credentialId, out var passkey) || passkey.SignCount != expectedSignCount)
                return Task.FromResult(false);
            _passkeys[credentialId] = passkey with { SignCount = signCount, BackedUp = backedUp, LastUsedAt = usedAt };
            return Task.FromResult(true);
        }
    }

    /// <inheritdoc/>
    public Task<bool> RenameAsync(string userId, string credentialId, string name, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (!_passkeys.TryGetValue(credentialId, out var passkey) || passkey.UserId != userId)
                return Task.FromResult(false);
            _passkeys[credentialId] = passkey with { Name = name };
            return Task.FromResult(true);
        }
    }

    /// <inheritdoc/>
    public Task<bool> DeleteAsync(string userId, string credentialId, CancellationToken cancellationToken)
    {
        lock (_lock)
            return Task.FromResult(_passkeys.TryGetValue(credentialId, out var passkey) && passkey.UserId == userId && _passkeys.TryRemove(credentialId, out _));
    }
}

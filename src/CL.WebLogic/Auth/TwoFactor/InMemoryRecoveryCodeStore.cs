using System.Collections.Concurrent;
using CL.WebLogic.Security.Services;

namespace CL.WebLogic.Auth.TwoFactor;

/// <summary>Development recovery code store (process memory).</summary>
public sealed class InMemoryRecoveryCodeStore : IWebRecoveryCodeStore, IWebDevelopmentStore
{
    private readonly ConcurrentDictionary<string, HashSet<string>> _codes = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public Task ReplaceAsync(string userId, IReadOnlyList<string> codeHashes, CancellationToken cancellationToken)
    {
        _codes[userId] = new HashSet<string>(codeHashes, StringComparer.Ordinal);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<bool> TryConsumeAsync(string userId, string codeHash, CancellationToken cancellationToken)
    {
        if (!_codes.TryGetValue(userId, out var set))
            return Task.FromResult(false);
        lock (set)
            return Task.FromResult(set.Remove(codeHash));
    }

    /// <inheritdoc/>
    public Task<int> CountAsync(string userId, CancellationToken cancellationToken)
    {
        if (!_codes.TryGetValue(userId, out var set))
            return Task.FromResult(0);
        lock (set)
            return Task.FromResult(set.Count);
    }
}

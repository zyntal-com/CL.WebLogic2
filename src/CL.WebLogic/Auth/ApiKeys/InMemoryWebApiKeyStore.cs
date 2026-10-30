using System.Collections.Concurrent;
using CL.WebLogic.Security.Services;

namespace CL.WebLogic.Auth.ApiKeys;

/// <summary>Development API key store (process memory).</summary>
public sealed class InMemoryWebApiKeyStore : IWebApiKeyStore, IWebDevelopmentStore
{
    private readonly ConcurrentDictionary<string, WebApiKey> _keys = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public Task<WebApiKey?> FindByPrefixAsync(string prefix, CancellationToken cancellationToken) =>
        Task.FromResult(_keys.Values.FirstOrDefault(k => string.Equals(k.Prefix, prefix, StringComparison.Ordinal)));

    /// <inheritdoc/>
    public Task<WebApiKey?> FindByIdAsync(string id, CancellationToken cancellationToken) =>
        Task.FromResult(_keys.TryGetValue(id, out var key) ? key : null);

    /// <inheritdoc/>
    public Task CreateAsync(WebApiKey key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (!_keys.TryAdd(key.Id, key))
            throw new InvalidOperationException($"API key '{key.Id}' already exists.");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task UpdateAsync(WebApiKey key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        _keys.AddOrUpdate(key.Id, key, (_, stored) => stored.RevokedAt is { } revoked && key.RevokedAt is null ? key with { RevokedAt = revoked } : key);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<WebApiKey>> ListAsync(string? userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<WebApiKey>>([.. _keys.Values.Where(k => userId is null || k.UserId == userId).OrderBy(k => k.CreatedAt)]);
}

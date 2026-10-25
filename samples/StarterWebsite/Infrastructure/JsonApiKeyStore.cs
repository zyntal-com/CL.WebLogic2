using CL.WebLogic.Auth.ApiKeys;

namespace StarterWebsite.Infrastructure;

/// <summary>API keys in <c>api-keys.json</c> (only the SHA-256 of each secret is stored).</summary>
public sealed class JsonApiKeyStore : IWebApiKeyStore, IDisposable
{
    private readonly JsonFileStore<WebApiKey> _keys;

    /// <summary>Stores in <paramref name="folder"/>.</summary>
    public JsonApiKeyStore(StarterDataFolder folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        _keys = new JsonFileStore<WebApiKey>(folder.File("api-keys.json"));
    }

    /// <inheritdoc/>
    public async Task<WebApiKey?> FindByPrefixAsync(string prefix, CancellationToken cancellationToken) =>
        (await _keys.WhereAsync(k => string.Equals(k.Prefix, prefix, StringComparison.Ordinal), cancellationToken).ConfigureAwait(false)).FirstOrDefault();

    /// <inheritdoc/>
    public Task<WebApiKey?> FindByIdAsync(string id, CancellationToken cancellationToken) => _keys.GetAsync(id, cancellationToken);

    /// <inheritdoc/>
    public Task CreateAsync(WebApiKey key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _keys.UpdateAsync(items => items.TryAdd(key.Id, key)
            ? (true, true)
            : throw new InvalidOperationException($"API key '{key.Id}' already exists."), cancellationToken);
    }

    /// <inheritdoc/>
    public Task UpdateAsync(WebApiKey key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _keys.SetAsync(key.Id, key, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WebApiKey>> ListAsync(string? userId, CancellationToken cancellationToken) =>
        [.. (await _keys.WhereAsync(k => userId is null || k.UserId == userId, cancellationToken).ConfigureAwait(false)).OrderBy(k => k.CreatedAt)];

    /// <inheritdoc/>
    public void Dispose() => _keys.Dispose();
}

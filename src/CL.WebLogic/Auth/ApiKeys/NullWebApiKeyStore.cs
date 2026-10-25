namespace CL.WebLogic.Auth.ApiKeys;

/// <summary>Default when the app registered no API key store: no key authenticates and none can be created.</summary>
public sealed class NullWebApiKeyStore : IWebApiKeyStore
{
    /// <inheritdoc/>
    public Task<WebApiKey?> FindByPrefixAsync(string prefix, CancellationToken cancellationToken) => Task.FromResult<WebApiKey?>(null);

    /// <inheritdoc/>
    public Task<WebApiKey?> FindByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult<WebApiKey?>(null);

    /// <inheritdoc/>
    public Task CreateAsync(WebApiKey key, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("No API key store is registered. Call web.UseApiKeyStore<T>().");

    /// <inheritdoc/>
    public Task UpdateAsync(WebApiKey key, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task<IReadOnlyList<WebApiKey>> ListAsync(string? userId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<WebApiKey>>([]);
}

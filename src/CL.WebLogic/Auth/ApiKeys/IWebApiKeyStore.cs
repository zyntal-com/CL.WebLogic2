namespace CL.WebLogic.Auth.ApiKeys;

/// <summary>API key persistence, supplied by the app.</summary>
public interface IWebApiKeyStore
{
    /// <summary>Finds a key by its prefix.</summary>
    Task<WebApiKey?> FindByPrefixAsync(string prefix, CancellationToken cancellationToken);

    /// <summary>Finds a key by id.</summary>
    Task<WebApiKey?> FindByIdAsync(string id, CancellationToken cancellationToken);

    /// <summary>Stores a new key.</summary>
    Task CreateAsync(WebApiKey key, CancellationToken cancellationToken);

    /// <summary>
    /// Saves a changed key (last use, revocation). Revocation is sticky: when the stored key is
    /// revoked, keep its <c>RevokedAt</c> even if <paramref name="key"/> (a stale copy written by a
    /// concurrent "last used" update) has none, or a revoked key comes back to life.
    /// </summary>
    Task UpdateAsync(WebApiKey key, CancellationToken cancellationToken);

    /// <summary>Keys of a user (or all keys when <paramref name="userId"/> is null).</summary>
    Task<IReadOnlyList<WebApiKey>> ListAsync(string? userId, CancellationToken cancellationToken);
}

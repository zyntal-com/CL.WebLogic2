using CL.WebLogic.Auth.ApiKeys;
using CL.WebLogic.Data.Entities;
using CL.WebLogic.Data.Services;

namespace CL.WebLogic.Data.Stores;

/// <summary>API keys in <c>wl_api_keys</c> (only the hash of the secret is ever stored).</summary>
public sealed class DataWebApiKeyStore : IWebApiKeyStore
{
    private readonly WebDataDatabase _db;

    /// <summary>Creates the store.</summary>
    public DataWebApiKeyStore(WebDataDatabase db) => _db = db ?? throw new ArgumentNullException(nameof(db));

    /// <inheritdoc/>
    public async Task<WebApiKey?> FindByPrefixAsync(string prefix, CancellationToken cancellationToken)
    {
        var row = WebDataDatabase.Value(await _db.Query<ApiKeyRow>().Where(k => k.Prefix == prefix).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false));
        return row is null ? null : WebDataJson.Read<WebApiKey>(row.RecordJson);
    }

    /// <inheritdoc/>
    public async Task<WebApiKey?> FindByIdAsync(string id, CancellationToken cancellationToken)
    {
        var row = WebDataDatabase.Value(await _db.Query<ApiKeyRow>().Where(r => r.Id == id).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false));
        return row is null ? null : WebDataJson.Read<WebApiKey>(row.RecordJson);
    }

    /// <inheritdoc/>
    public async Task CreateAsync(WebApiKey key, CancellationToken cancellationToken) =>
        WebDataDatabase.Value(await _db.Repository<ApiKeyRow>().InsertAsync(ToRow(key), cancellationToken).ConfigureAwait(false));

    /// <inheritdoc/>
    public async Task UpdateAsync(WebApiKey key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        // Revocation is sticky: a stale copy from a concurrent "last used" update must not undo it.
        if (key.RevokedAt is null && await FindByIdAsync(key.Id, cancellationToken).ConfigureAwait(false) is { RevokedAt: { } revoked })
            key = key with { RevokedAt = revoked };
        WebDataDatabase.Value(await _db.Repository<ApiKeyRow>().UpdateAsync(ToRow(key), cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WebApiKey>> ListAsync(string? userId, CancellationToken cancellationToken)
    {
        var rows = userId is null
            ? WebDataDatabase.Value(await _db.Repository<ApiKeyRow>().GetAllAsync(cancellationToken).ConfigureAwait(false))
            : WebDataDatabase.Value(await _db.Query<ApiKeyRow>().Where(k => k.UserId == userId).ToListAsync(cancellationToken).ConfigureAwait(false));
        return [.. rows.Select(r => WebDataJson.Read<WebApiKey>(r.RecordJson))];
    }

    private static ApiKeyRow ToRow(WebApiKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return new ApiKeyRow { Id = key.Id, Prefix = key.Prefix, UserId = key.UserId, RecordJson = WebDataJson.Write(key) };
    }
}

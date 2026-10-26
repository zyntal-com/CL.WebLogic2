using CL.WebLogic.Auth.ApiKeys;
using CL.WebLogic.MySql.Entities;
using CL.WebLogic.MySql.Services;

namespace CL.WebLogic.MySql.Stores;

/// <summary>API keys in <c>wl_api_keys</c> (only the hash of the secret is ever stored).</summary>
public sealed class MySqlWebApiKeyStore : IWebApiKeyStore
{
    private readonly WebMySqlDatabase _db;

    /// <summary>Creates the store.</summary>
    public MySqlWebApiKeyStore(WebMySqlDatabase db) => _db = db ?? throw new ArgumentNullException(nameof(db));

    /// <inheritdoc/>
    public async Task<WebApiKey?> FindByPrefixAsync(string prefix, CancellationToken cancellationToken)
    {
        var row = WebMySqlDatabase.Value(await _db.Query<ApiKeyRow>().Where(k => k.Prefix == prefix).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false));
        return row is null ? null : WebMySqlJson.Read<WebApiKey>(row.RecordJson);
    }

    /// <inheritdoc/>
    public async Task<WebApiKey?> FindByIdAsync(string id, CancellationToken cancellationToken)
    {
        var row = WebMySqlDatabase.Value(await _db.Repository<ApiKeyRow>().GetByIdAsync(id, cancellationToken).ConfigureAwait(false));
        return row is null ? null : WebMySqlJson.Read<WebApiKey>(row.RecordJson);
    }

    /// <inheritdoc/>
    public async Task CreateAsync(WebApiKey key, CancellationToken cancellationToken) =>
        WebMySqlDatabase.Value(await _db.Repository<ApiKeyRow>().InsertAsync(ToRow(key), cancellationToken).ConfigureAwait(false));

    /// <inheritdoc/>
    public async Task UpdateAsync(WebApiKey key, CancellationToken cancellationToken) =>
        WebMySqlDatabase.Value(await _db.Repository<ApiKeyRow>().UpdateAsync(ToRow(key), cancellationToken).ConfigureAwait(false));

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WebApiKey>> ListAsync(string? userId, CancellationToken cancellationToken)
    {
        var rows = userId is null
            ? WebMySqlDatabase.Value(await _db.Repository<ApiKeyRow>().GetAllAsync(cancellationToken).ConfigureAwait(false))
            : WebMySqlDatabase.Value(await _db.Query<ApiKeyRow>().Where(k => k.UserId == userId).ToListAsync(cancellationToken).ConfigureAwait(false));
        return [.. rows.Select(r => WebMySqlJson.Read<WebApiKey>(r.RecordJson))];
    }

    private static ApiKeyRow ToRow(WebApiKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return new ApiKeyRow { Id = key.Id, Prefix = key.Prefix, UserId = key.UserId, RecordJson = WebMySqlJson.Write(key) };
    }
}

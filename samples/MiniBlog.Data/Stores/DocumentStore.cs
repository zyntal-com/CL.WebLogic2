using System.Text.Json;
using CL.Data.Query;
using MiniBlog.Data.Entities;

namespace MiniBlog.Data.Stores;

/// <summary>Typed reads and writes of <see cref="DocumentEntity"/> rows.</summary>
public sealed class DocumentStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly BlogDatabase _db;

    /// <summary>Creates the store.</summary>
    public DocumentStore(BlogDatabase db) => _db = db;

    /// <summary>The document and its version, or (default, 0).</summary>
    public async Task<(T? Value, long Version)> GetAsync<T>(string collection, string key, CancellationToken cancellationToken)
    {
        var row = await _db.Db.Repository<DocumentEntity>().GetByIdAsync(Id(collection, key), cancellationToken).ConfigureAwait(false);
        if (row.IsFailure)
            throw new InvalidOperationException(row.Error!.Message);
        return row.Value is null ? (default, 0) : (JsonSerializer.Deserialize<T>(row.Value.Json, Json), row.Value.Version);
    }

    /// <summary>Writes unconditionally (last write wins).</summary>
    public async Task SetAsync<T>(string collection, string key, T value, CancellationToken cancellationToken)
    {
        var entity = new DocumentEntity { Id = Id(collection, key), Collection = collection, Json = JsonSerializer.Serialize(value, Json), Version = 1, UpdatedUtc = DateTime.UtcNow };
        var saved = await _db.Db.Repository<DocumentEntity>().UpsertAsync(entity, ct: cancellationToken).ConfigureAwait(false);
        if (saved.IsFailure)
            throw new InvalidOperationException(saved.Error!.Message);
    }

    /// <summary>
    /// Writes only when the stored version is <paramref name="expectedVersion"/> (0 = absent);
    /// returns the new version, or null on a conflict.
    /// </summary>
    public async Task<long?> TrySetAsync<T>(string collection, string key, T value, long expectedVersion, CancellationToken cancellationToken)
    {
        var id = Id(collection, key);
        var json = JsonSerializer.Serialize(value, Json);
        if (expectedVersion == 0)
        {
            var inserted = await _db.Db.Repository<DocumentEntity>()
                .InsertIgnoreAsync(new DocumentEntity { Id = id, Collection = collection, Json = json, Version = 1, UpdatedUtc = DateTime.UtcNow }, cancellationToken).ConfigureAwait(false);
            if (inserted.IsFailure)
                throw new InvalidOperationException(inserted.Error!.Message);
            return inserted.Value == InsertOutcome.Inserted ? 1 : null;
        }

        var next = expectedVersion + 1;
        var now = DateTime.UtcNow;
        var updated = await _db.Db.Query<DocumentEntity>()
            .Where(d => d.Id == id && d.Version == expectedVersion)
            .UpdateAsync(d => new DocumentEntity { Json = json, Version = next, UpdatedUtc = now }, cancellationToken)
            .ConfigureAwait(false);
        if (updated.IsFailure)
            throw new InvalidOperationException(updated.Error!.Message);
        return updated.Value == 1 ? expectedVersion + 1 : null;
    }

    private static string Id(string collection, string key) => collection + ":" + key;
}

using CL.Data.Execution;
using CL.Data.Hosting;
using CL.Data.Mapping;
using CodeLogic;
using MiniBlog.Data.Entities;

namespace MiniBlog.Data;

/// <summary>Access to the CL.Data connection MiniBlog uses.</summary>
public sealed class BlogDatabase
{
    /// <summary>Uses the named CL.Data connection.</summary>
    public BlogDatabase(string connectionName = "Default") => ConnectionName = connectionName;

    /// <summary>The CL.Data connection name (config.data.json → connections).</summary>
    public string ConnectionName { get; }

    /// <summary>The connection.</summary>
    public Database Db
    {
        get
        {
            var data = Libraries.Get<DataLibrary>() ?? throw new InvalidOperationException("CL.Data is not loaded.");
            var db = data.Db(ConnectionName);
            return db.IsSuccess ? db.Value! : throw new InvalidOperationException("MiniBlog database: " + db.Error!.Message);
        }
    }

    /// <summary>Creates or updates every MiniBlog table (CL.Data schema sync; unchanged tables are skipped).</summary>
    public async Task SyncSchemaAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<EntityMap> maps =
        [
            EntityAccessorRegistry.Get<PostEntity>().ValueOrThrow().Map,
            EntityAccessorRegistry.Get<DocumentEntity>().ValueOrThrow().Map,
        ];
        var result = await Db.Schema.SyncAsync(maps, ct: cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
            throw new InvalidOperationException("MiniBlog schema sync failed: " + result.Error!.Message);
    }
}

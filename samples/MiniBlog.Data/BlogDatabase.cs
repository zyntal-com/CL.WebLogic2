using CL.MySQL2;
using CodeLogic;

namespace MiniBlog.Data;

/// <summary>Access to the CL.MySQL2 library and the connection MiniBlog uses.</summary>
public sealed class BlogDatabase
{
    private MySQL2Library? _mysql;

    /// <summary>Uses the named CL.MySQL2 connection.</summary>
    public BlogDatabase(string connectionId = "Default") => ConnectionId = connectionId;

    /// <summary>The CL.MySQL2 connection id (config.mysql.json → databases).</summary>
    public string ConnectionId { get; }

    /// <summary>The loaded library.</summary>
    public MySQL2Library MySql => _mysql ??= Libraries.Get<MySQL2Library>()
        ?? throw new InvalidOperationException("CL.MySQL2 is not loaded.");

    /// <summary>Creates or updates every MiniBlog table (additive; CL.MySQL2 skips unchanged tables).</summary>
    public async Task SyncSchemaAsync(CancellationToken cancellationToken)
    {
        var result = await MySql.SyncSchemaAsync(
            [typeof(Entities.PostEntity), typeof(Entities.DocumentEntity)],
            false, ConnectionId, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
            throw new InvalidOperationException("MiniBlog schema sync failed: " + result.Error!.Message);
    }
}

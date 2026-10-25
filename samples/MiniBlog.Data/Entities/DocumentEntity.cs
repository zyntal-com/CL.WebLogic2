using CL.MySQL2.Models;

namespace MiniBlog.Data.Entities;

/// <summary>
/// Small JSON documents keyed by collection and key (recovery codes, widget settings, dashboard
/// layouts, seed markers), with a version for optimistic writes.
/// </summary>
[Table(Name = "miniblog_documents")]
[CompositeIndex("ix_miniblog_documents_collection", "collection", "updated_utc")]
public sealed class DocumentEntity
{
    /// <summary><c>collection:key</c>.</summary>
    [Column(Name = "id", DataType = DataType.VarChar, Size = 255, Primary = true, NotNull = true)]
    public string Id { get; set; } = string.Empty;

    /// <summary>The collection.</summary>
    [Column(Name = "collection", DataType = DataType.VarChar, Size = 40, NotNull = true)]
    public string Collection { get; set; } = string.Empty;

    /// <summary>The JSON payload.</summary>
    [Column(Name = "json", DataType = DataType.MediumText, NotNull = true)]
    public string Json { get; set; } = "{}";

    /// <summary>Incremented on every write.</summary>
    [Column(Name = "version", DataType = DataType.BigInt, NotNull = true)]
    public long Version { get; set; }

    /// <summary>Last write (UTC).</summary>
    [Column(Name = "updated_utc", DataType = DataType.DateTime, NotNull = true)]
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}

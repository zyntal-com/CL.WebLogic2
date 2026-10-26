using CL.MySQL2.Models;

namespace CL.WebLogic.MySql.Entities;

/// <summary>An API key (hash only) with its record as JSON.</summary>
[Table(Name = "wl_api_keys", Collation = "utf8mb4_bin")]
public sealed class ApiKeyRow
{
    /// <summary>Key id.</summary>
    [Column(Name = "id", DataType = DataType.VarChar, Size = 64, Primary = true, NotNull = true)]
    public string Id { get; set; } = string.Empty;

    /// <summary>Public prefix used for lookups.</summary>
    [Column(Name = "prefix", DataType = DataType.VarChar, Size = 64, NotNull = true, Unique = true)]
    public string Prefix { get; set; } = string.Empty;

    /// <summary>Owner, if any.</summary>
    [Column(Name = "user_id", DataType = DataType.VarChar, Size = 64, Index = true)]
    public string? UserId { get; set; }

    /// <summary>The WebApiKey as JSON.</summary>
    [Column(Name = "record_json", DataType = DataType.Text, NotNull = true)]
    public string RecordJson { get; set; } = "{}";
}

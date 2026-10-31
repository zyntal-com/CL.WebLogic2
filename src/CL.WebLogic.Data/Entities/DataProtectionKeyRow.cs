using CL.Data;

namespace CL.WebLogic.Data.Entities;

/// <summary>One DataProtection key (XML, possibly encrypted).</summary>
[Table(Name = "wl_dataprotection_keys")]
[MySqlTable(Collation = WebDataCollations.MySql)]
public sealed class DataProtectionKeyRow
{
    /// <summary>Friendly name (key id).</summary>
    [Column(Name = "name", DataType = DataType.String, Size = 128, Primary = true, NotNull = true)]
    [SqlServerColumn(Collation = WebDataCollations.SqlServer)]
    public string Name { get; set; } = string.Empty;

    /// <summary>The key element.</summary>
    [Column(Name = "xml", DataType = DataType.Text, NotNull = true)]
    public string Xml { get; set; } = string.Empty;

    /// <summary>Stored (UTC).</summary>
    [Column(Name = "created_utc", DataType = DataType.DateTime, NotNull = true)]
    public DateTime CreatedUtc { get; set; }
}

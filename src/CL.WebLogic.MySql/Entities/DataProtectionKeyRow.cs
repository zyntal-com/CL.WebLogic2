using CL.MySQL2.Models;

namespace CL.WebLogic.MySql.Entities;

/// <summary>One DataProtection key (XML, possibly encrypted).</summary>
[Table(Name = "wl_dataprotection_keys", Collation = "utf8mb4_bin")]
public sealed class DataProtectionKeyRow
{
    /// <summary>Friendly name (key id).</summary>
    [Column(Name = "name", DataType = DataType.VarChar, Size = 128, Primary = true, NotNull = true)]
    public string Name { get; set; } = string.Empty;

    /// <summary>The key element.</summary>
    [Column(Name = "xml", DataType = DataType.MediumText, NotNull = true)]
    public string Xml { get; set; } = string.Empty;

    /// <summary>Stored (UTC).</summary>
    [Column(Name = "created_utc", DataType = DataType.DateTime, NotNull = true)]
    public DateTime CreatedUtc { get; set; }
}

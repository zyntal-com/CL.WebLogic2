using CL.Data;

namespace CL.WebLogic.Data.Entities;

/// <summary>A spent single-use key (TOTP step, link token) until it expires.</summary>
[Table(Name = "wl_one_time_use")]
[MySqlTable(Collation = WebDataCollations.MySql)]
public sealed class OneTimeUseRow
{
    /// <summary>SHA-256 (hex) of the key.</summary>
    [Column(Name = "use_key", DataType = DataType.FixedString, Size = 64, Primary = true, NotNull = true)]
    [SqlServerColumn(Collation = WebDataCollations.SqlServer)]
    public string UseKey { get; set; } = string.Empty;

    /// <summary>When the key may be forgotten (UTC).</summary>
    [Column(Name = "expires_utc", DataType = DataType.DateTime, NotNull = true, Index = true)]
    public DateTime ExpiresUtc { get; set; }
}

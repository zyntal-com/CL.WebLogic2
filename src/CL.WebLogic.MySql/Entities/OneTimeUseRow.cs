using CL.MySQL2.Models;

namespace CL.WebLogic.MySql.Entities;

/// <summary>A spent single-use key (TOTP step, link token) until it expires.</summary>
[Table(Name = "wl_one_time_use", Collation = "utf8mb4_bin")]
public sealed class OneTimeUseRow
{
    /// <summary>SHA-256 (hex) of the key.</summary>
    [Column(Name = "use_key", DataType = DataType.Char, Size = 64, Primary = true, NotNull = true)]
    public string UseKey { get; set; } = string.Empty;

    /// <summary>When the key may be forgotten (UTC).</summary>
    [Column(Name = "expires_utc", DataType = DataType.DateTime, NotNull = true, Index = true)]
    public DateTime ExpiresUtc { get; set; }
}

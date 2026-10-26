using CL.MySQL2.Models;

namespace CL.WebLogic.MySql.Entities;

/// <summary>A token bucket.</summary>
[Table(Name = "wl_rate_limits", Collation = "utf8mb4_bin")]
public sealed class RateLimitRow
{
    /// <summary>Policy and partition key.</summary>
    [Column(Name = "bucket_key", DataType = DataType.VarChar, Size = 300, Primary = true, NotNull = true)]
    public string BucketKey { get; set; } = string.Empty;

    /// <summary>Tokens left after the last request.</summary>
    [Column(Name = "tokens", DataType = DataType.Double, NotNull = true)]
    public double Tokens { get; set; }

    /// <summary>Whether the last request was allowed (scratch column for the atomic update).</summary>
    [Column(Name = "allowed", DataType = DataType.TinyInt, NotNull = true)]
    public int Allowed { get; set; }

    /// <summary>Last update (Unix milliseconds).</summary>
    [Column(Name = "updated_ms", DataType = DataType.BigInt, NotNull = true, Index = true)]
    public long UpdatedMs { get; set; }
}

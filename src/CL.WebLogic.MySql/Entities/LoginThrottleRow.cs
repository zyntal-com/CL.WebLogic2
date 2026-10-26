using CL.MySQL2.Models;

namespace CL.WebLogic.MySql.Entities;

/// <summary>Failed sign-in counter for a login or a client address.</summary>
[Table(Name = "wl_login_throttle", Collation = "utf8mb4_bin")]
public sealed class LoginThrottleRow
{
    /// <summary>login:{NAME} or ip:{address key}.</summary>
    [Column(Name = "throttle_key", DataType = DataType.VarChar, Size = 300, Primary = true, NotNull = true)]
    public string ThrottleKey { get; set; } = string.Empty;

    /// <summary>Failures in the window.</summary>
    [Column(Name = "failures", DataType = DataType.Int, NotNull = true)]
    public int Failures { get; set; }

    /// <summary>Last failure (UTC).</summary>
    [Column(Name = "last_utc", DataType = DataType.DateTime, NotNull = true)]
    public DateTime LastUtc { get; set; }

    /// <summary>When the counter resets (UTC).</summary>
    [Column(Name = "expires_utc", DataType = DataType.DateTime, NotNull = true, Index = true)]
    public DateTime ExpiresUtc { get; set; }
}

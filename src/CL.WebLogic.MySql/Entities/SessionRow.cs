using CL.MySQL2.Models;

namespace CL.WebLogic.MySql.Entities;

/// <summary>A session: indexed columns for the queries plus the full record as JSON.</summary>
[Table(Name = "wl_sessions", Collation = "utf8mb4_bin")]
public sealed class SessionRow
{
    /// <summary>SHA-256 of the session id (raw ids are never stored).</summary>
    [Column(Name = "id_hash", DataType = DataType.VarChar, Size = 96, Primary = true, NotNull = true)]
    public string IdHash { get; set; } = string.Empty;

    /// <summary>Signed-in user, if any.</summary>
    [Column(Name = "user_id", DataType = DataType.VarChar, Size = 64, Index = true)]
    public string? UserId { get; set; }

    /// <summary>Expiry (UTC).</summary>
    [Column(Name = "expires_utc", DataType = DataType.DateTime, NotNull = true, Index = true)]
    public DateTime ExpiresUtc { get; set; }

    /// <summary>The WebSessionRecord as JSON.</summary>
    [Column(Name = "record_json", DataType = DataType.MediumText, NotNull = true)]
    public string RecordJson { get; set; } = "{}";
}

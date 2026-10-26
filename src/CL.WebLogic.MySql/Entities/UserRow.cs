using CL.MySQL2.Models;

namespace CL.WebLogic.MySql.Entities;

/// <summary>A user: lookup columns, the lockout counters (updated atomically) and the full record as JSON.</summary>
[Table(Name = "wl_users", Collation = "utf8mb4_bin")]
public sealed class UserRow
{
    /// <summary>User id.</summary>
    [Column(Name = "id", DataType = DataType.VarChar, Size = 64, Primary = true, NotNull = true)]
    public string Id { get; set; } = string.Empty;

    /// <summary>Upper-case user name (unique).</summary>
    [Column(Name = "normalized_user_name", DataType = DataType.VarChar, Size = 256, NotNull = true, Unique = true)]
    public string NormalizedUserName { get; set; } = string.Empty;

    /// <summary>Upper-case e-mail address (unique when set).</summary>
    [Column(Name = "normalized_email", DataType = DataType.VarChar, Size = 320, Unique = true)]
    public string? NormalizedEmail { get; set; }

    /// <summary>Tenant.</summary>
    [Column(Name = "tenant_id", DataType = DataType.VarChar, Size = 128, Index = true)]
    public string? TenantId { get; set; }

    /// <summary>Failed sign-ins since the last success (authoritative over the JSON).</summary>
    [Column(Name = "failed_access_count", DataType = DataType.Int, NotNull = true)]
    public int FailedAccessCount { get; set; }

    /// <summary>Locked until (UTC; authoritative over the JSON).</summary>
    [Column(Name = "lockout_end_utc", DataType = DataType.DateTime)]
    public DateTime? LockoutEndUtc { get; set; }

    /// <summary>Created (UTC).</summary>
    [Column(Name = "created_utc", DataType = DataType.DateTime, NotNull = true, Index = true)]
    public DateTime CreatedUtc { get; set; }

    /// <summary>The WebUser as JSON.</summary>
    [Column(Name = "record_json", DataType = DataType.MediumText, NotNull = true)]
    public string RecordJson { get; set; } = "{}";
}

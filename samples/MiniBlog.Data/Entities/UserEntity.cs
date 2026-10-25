using CL.MySQL2.Models;

namespace MiniBlog.Data.Entities;

/// <summary>A WebLogic user row (lists as JSON; passwords as PBKDF2 hashes from the toolkit).</summary>
[Table(Name = "miniblog_users")]
public sealed class UserEntity
{
    /// <summary>Id.</summary>
    [Column(Name = "id", DataType = DataType.VarChar, Size = 64, Primary = true, NotNull = true)]
    public string Id { get; set; } = string.Empty;

    /// <summary>Login name.</summary>
    [Column(Name = "user_name", DataType = DataType.VarChar, Size = 64, NotNull = true, Unique = true)]
    public string UserName { get; set; } = string.Empty;

    /// <summary>E-mail (unique when set).</summary>
    [Column(Name = "email", DataType = DataType.VarChar, Size = 254, Unique = true)]
    public string? Email { get; set; }

    /// <summary>Whether the e-mail address is confirmed.</summary>
    [Column(Name = "email_confirmed", DataType = DataType.TinyInt, NotNull = true)]
    public bool EmailConfirmed { get; set; }

    /// <summary>Password hash.</summary>
    [Column(Name = "password_hash", DataType = DataType.VarChar, Size = 300)]
    public string? PasswordHash { get; set; }

    /// <summary>Security stamp (changes invalidate sessions and tokens).</summary>
    [Column(Name = "security_stamp", DataType = DataType.VarChar, Size = 64, NotNull = true)]
    public string SecurityStamp { get; set; } = string.Empty;

    /// <summary>Groups (JSON array).</summary>
    [Column(Name = "groups_json", DataType = DataType.Text, NotNull = true)]
    public string GroupsJson { get; set; } = "[]";

    /// <summary>Permissions (JSON array).</summary>
    [Column(Name = "permissions_json", DataType = DataType.Text, NotNull = true)]
    public string PermissionsJson { get; set; } = "[]";

    /// <summary>Disabled accounts cannot sign in.</summary>
    [Column(Name = "is_disabled", DataType = DataType.TinyInt, NotNull = true)]
    public bool IsDisabled { get; set; }

    /// <summary>Must choose a new password at next sign-in.</summary>
    [Column(Name = "requires_password_change", DataType = DataType.TinyInt, NotNull = true)]
    public bool RequiresPasswordChange { get; set; }

    /// <summary>Locked until (UTC).</summary>
    [Column(Name = "lockout_end_utc", DataType = DataType.DateTime)]
    public DateTime? LockoutEndUtc { get; set; }

    /// <summary>Failed attempts since the last success.</summary>
    [Column(Name = "failed_access_count", DataType = DataType.Int, NotNull = true)]
    public int FailedAccessCount { get; set; }

    /// <summary>Protected TOTP secret.</summary>
    [Column(Name = "two_factor_secret", DataType = DataType.VarChar, Size = 600)]
    public string? TwoFactorSecret { get; set; }

    /// <summary>Created (UTC).</summary>
    [Column(Name = "created_utc", DataType = DataType.DateTime, NotNull = true)]
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

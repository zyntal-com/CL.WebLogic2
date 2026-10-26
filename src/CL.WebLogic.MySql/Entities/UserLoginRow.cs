using CL.MySQL2.Models;

namespace CL.WebLogic.MySql.Entities;

/// <summary>An external login linked to a user.</summary>
[Table(Name = "wl_user_logins", Collation = "utf8mb4_bin")]
public sealed class UserLoginRow
{
    /// <summary>Lower-case provider, a colon and the provider key.</summary>
    [Column(Name = "login_key", DataType = DataType.VarChar, Size = 400, Primary = true, NotNull = true)]
    public string LoginKey { get; set; } = string.Empty;

    /// <summary>User id.</summary>
    [Column(Name = "user_id", DataType = DataType.VarChar, Size = 64, NotNull = true, Index = true)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>Linked (UTC).</summary>
    [Column(Name = "created_utc", DataType = DataType.DateTime, NotNull = true)]
    public DateTime CreatedUtc { get; set; }
}

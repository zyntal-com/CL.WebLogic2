using CL.Data;

namespace CL.WebLogic.Data.Entities;

/// <summary>An external login linked to a user.</summary>
[Table(Name = "wl_user_logins")]
[MySqlTable(Collation = WebDataCollations.MySql)]
public sealed class UserLoginRow
{
    /// <summary>Lower-case provider, a colon and the provider key.</summary>
    [Column(Name = "login_key", DataType = DataType.String, Size = 400, Primary = true, NotNull = true)]
    [SqlServerColumn(Collation = WebDataCollations.SqlServer)]
    public string LoginKey { get; set; } = string.Empty;

    /// <summary>User id.</summary>
    [Column(Name = "user_id", DataType = DataType.String, Size = 64, NotNull = true, Index = true)]
    [SqlServerColumn(Collation = WebDataCollations.SqlServer)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>Linked (UTC).</summary>
    [Column(Name = "created_utc", DataType = DataType.DateTime, NotNull = true)]
    public DateTime CreatedUtc { get; set; }
}

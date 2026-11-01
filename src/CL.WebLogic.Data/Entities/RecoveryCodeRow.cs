using CL.Data;

namespace CL.WebLogic.Data.Entities;

/// <summary>One unused recovery code (hashed).</summary>
[Table(Name = "wl_recovery_codes")]
[MySqlTable(Collation = WebDataCollations.MySql)]
public sealed class RecoveryCodeRow
{
    /// <summary>User id, a colon and the code hash.</summary>
    [Column(Name = "code_key", DataType = DataType.String, Size = 300, Primary = true, NotNull = true)]
    [SqlServerColumn(Collation = WebDataCollations.SqlServer)]
    public string CodeKey { get; set; } = string.Empty;

    /// <summary>User id.</summary>
    [Column(Name = "user_id", DataType = DataType.String, Size = 64, NotNull = true, Index = true)]
    [SqlServerColumn(Collation = WebDataCollations.SqlServer)]
    public string UserId { get; set; } = string.Empty;
}

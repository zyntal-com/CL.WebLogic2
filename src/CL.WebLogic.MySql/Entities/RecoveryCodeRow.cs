using CL.MySQL2.Models;

namespace CL.WebLogic.MySql.Entities;

/// <summary>One unused recovery code (hashed).</summary>
[Table(Name = "wl_recovery_codes", Collation = "utf8mb4_bin")]
public sealed class RecoveryCodeRow
{
    /// <summary>User id, a colon and the code hash.</summary>
    [Column(Name = "code_key", DataType = DataType.VarChar, Size = 300, Primary = true, NotNull = true)]
    public string CodeKey { get; set; } = string.Empty;

    /// <summary>User id.</summary>
    [Column(Name = "user_id", DataType = DataType.VarChar, Size = 64, NotNull = true, Index = true)]
    public string UserId { get; set; } = string.Empty;
}

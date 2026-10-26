using CL.MySQL2.Models;

namespace CL.WebLogic.MySql.Entities;

/// <summary>A dashboard layout with its optimistic-concurrency version.</summary>
[Table(Name = "wl_dashboards", Collation = "utf8mb4_bin")]
public sealed class DashboardRow
{
    /// <summary>Owner key, a slash and the dashboard key.</summary>
    [Column(Name = "layout_key", DataType = DataType.VarChar, Size = 300, Primary = true, NotNull = true)]
    public string LayoutKey { get; set; } = string.Empty;

    /// <summary>Owner key.</summary>
    [Column(Name = "owner_key", DataType = DataType.VarChar, Size = 128, NotNull = true, Index = true)]
    public string OwnerKey { get; set; } = string.Empty;

    /// <summary>Version (incremented on every save).</summary>
    [Column(Name = "version", DataType = DataType.BigInt, NotNull = true)]
    public long Version { get; set; }

    /// <summary>The WebDashboardLayout as JSON.</summary>
    [Column(Name = "record_json", DataType = DataType.MediumText, NotNull = true)]
    public string RecordJson { get; set; } = "{}";
}

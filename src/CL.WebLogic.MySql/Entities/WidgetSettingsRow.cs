using CL.MySQL2.Models;

namespace CL.WebLogic.MySql.Entities;

/// <summary>Settings of one widget instance.</summary>
[Table(Name = "wl_widget_settings", Collation = "utf8mb4_bin")]
public sealed class WidgetSettingsRow
{
    /// <summary>Owner key, a slash and the instance id.</summary>
    [Column(Name = "settings_key", DataType = DataType.VarChar, Size = 300, Primary = true, NotNull = true)]
    public string SettingsKey { get; set; } = string.Empty;

    /// <summary>Owner key.</summary>
    [Column(Name = "owner_key", DataType = DataType.VarChar, Size = 128, NotNull = true, Index = true)]
    public string OwnerKey { get; set; } = string.Empty;

    /// <summary>The WebWidgetSettings as JSON.</summary>
    [Column(Name = "record_json", DataType = DataType.Text, NotNull = true)]
    public string RecordJson { get; set; } = "{}";
}

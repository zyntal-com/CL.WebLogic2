using CL.Data;

namespace CL.WebLogic.Data.Entities;

/// <summary>Settings of one widget instance.</summary>
[Table(Name = "wl_widget_settings")]
[MySqlTable(Collation = WebDataCollations.MySql)]
public sealed class WidgetSettingsRow
{
    /// <summary>Owner key, a slash and the instance id.</summary>
    [Column(Name = "settings_key", DataType = DataType.String, Size = 300, Primary = true, NotNull = true)]
    [SqlServerColumn(Collation = WebDataCollations.SqlServer)]
    public string SettingsKey { get; set; } = string.Empty;

    /// <summary>Owner key.</summary>
    [Column(Name = "owner_key", DataType = DataType.String, Size = 128, NotNull = true, Index = true)]
    [SqlServerColumn(Collation = WebDataCollations.SqlServer)]
    public string OwnerKey { get; set; } = string.Empty;

    /// <summary>The WebWidgetSettings as JSON.</summary>
    [Column(Name = "record_json", DataType = DataType.Text, NotNull = true)]
    public string RecordJson { get; set; } = "{}";
}

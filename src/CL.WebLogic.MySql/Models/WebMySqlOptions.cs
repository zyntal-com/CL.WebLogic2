namespace CL.WebLogic.MySql.Models;

/// <summary>Options for <c>web.UseMySqlStores(...)</c>.</summary>
public sealed class WebMySqlOptions
{
    /// <summary>The CL.MySQL2 connection id (<c>config.mysql.json</c> → <c>databases</c>).</summary>
    public string ConnectionId { get; set; } = "Default";

    /// <summary>Which stores to register (default: all). Leave out the ones your app owns.</summary>
    public WebMySqlStores Stores { get; set; } = WebMySqlStores.All;

    /// <summary>Create and update the <c>wl_*</c> tables at startup (additive; CL.MySQL2 schema sync).</summary>
    public bool SyncSchemaOnStart { get; set; } = true;

    /// <summary>How often expired rows (sessions, one-time keys, throttle and rate-limit rows, old audit) are deleted.</summary>
    public TimeSpan SweepInterval { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>Audit rows older than this are deleted by the sweep (zero = keep forever).</summary>
    public TimeSpan AuditRetention { get; set; } = TimeSpan.FromDays(90);
}

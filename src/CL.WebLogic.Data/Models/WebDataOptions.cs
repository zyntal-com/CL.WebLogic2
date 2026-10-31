namespace CL.WebLogic.Data.Models;

/// <summary>Options for <c>web.UseDataStores(...)</c>.</summary>
public sealed class WebDataOptions
{
    /// <summary>The CL.Data connection name (<c>config.data.json</c> → <c>connections</c>).</summary>
    public string ConnectionName { get; set; } = "Default";

    /// <summary>Which stores to register (default: all). Leave out the ones your app owns.</summary>
    public WebDataStores Stores { get; set; } = WebDataStores.All;

    /// <summary>
    /// Create and update the <c>wl_*</c> tables at startup (CL.Data schema sync at the connection's
    /// <c>syncMode</c>; changes the mode does not allow are logged as drift, never applied).
    /// </summary>
    public bool SyncSchemaOnStart { get; set; } = true;

    /// <summary>How often expired rows (one-time keys, throttle and rate-limit rows, old audit) are deleted.</summary>
    public TimeSpan SweepInterval { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>Audit rows older than this are deleted by the sweep (zero = keep forever).</summary>
    public TimeSpan AuditRetention { get; set; } = TimeSpan.FromDays(90);
}

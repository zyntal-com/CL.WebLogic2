using CodeLogic.Core.Configuration;

namespace CL.WebLogic.Configuration.Models;

/// <summary>API keys (<c>wl_{prefix}_{secret}</c>), sent in a header or as a bearer token.</summary>
public sealed class ApiKeyConfig
{
    /// <summary>Accept API keys.</summary>
    [ConfigField(Label = "Enabled", Group = "API Keys", Order = 50)]
    public bool Enabled { get; set; } = true;

    /// <summary>Header carrying the key (also accepted: <c>Authorization: Bearer wl_…</c> / <c>ApiKey wl_…</c>).</summary>
    [ConfigField(Label = "Header Name", Group = "API Keys", Order = 51, Collapsed = true)]
    public string HeaderName { get; set; } = "X-Api-Key";

    /// <summary>Minutes between "last used" updates per key.</summary>
    [ConfigField(Label = "Touch Interval (min)", Min = 1, Max = 1440, Group = "API Keys", Order = 52, Collapsed = true)]
    public int TouchIntervalMinutes { get; set; } = 5;
}

using CodeLogic.Core.Configuration;

namespace CL.WebLogic.Configuration.Models;

/// <summary>Server-side sessions. Only a random token goes into the cookie; the store keeps its SHA-256 hash.</summary>
public sealed class SessionConfig
{
    /// <summary>Cookie name over HTTPS (<c>__Host-</c> pins it to this host, path / and Secure).</summary>
    [ConfigField(Label = "Cookie Name", Description = "Over plain HTTP (development) the __Host- prefix is dropped.", Group = "Sessions", Order = 80, Collapsed = true)]
    public string CookieName { get; set; } = "__Host-wl_sid";

    /// <summary>Minutes without activity before a session ends.</summary>
    [ConfigField(Label = "Idle Timeout (min)", Min = 1, Max = 525600, Group = "Sessions", Order = 81)]
    public int IdleTimeoutMinutes { get; set; } = 60;

    /// <summary>Hours after creation when a session ends regardless of activity.</summary>
    [ConfigField(Label = "Absolute Timeout (h)", Min = 1, Max = 8760, Group = "Sessions", Order = 82)]
    public int AbsoluteTimeoutHours { get; set; } = 24 * 14;

    /// <summary>Minimum seconds between "last seen" updates (limits store writes).</summary>
    [ConfigField(Label = "Touch Interval (s)", Min = 1, Max = 3600, Group = "Sessions", Order = 83, Collapsed = true)]
    public int TouchIntervalSeconds { get; set; } = 60;

    /// <summary>End the session when the client IP changes (breaks mobile users; off by default).</summary>
    [ConfigField(Label = "Bind To Client IP", Group = "Sessions", Order = 84, Collapsed = true)]
    public bool BindToClientIp { get; set; }
}

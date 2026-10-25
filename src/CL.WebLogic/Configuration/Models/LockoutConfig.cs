using CodeLogic.Core.Configuration;

namespace CL.WebLogic.Configuration.Models;

/// <summary>Account lockout and per-login / per-IP throttling of failed sign-ins.</summary>
public sealed class LockoutConfig
{
    /// <summary>Failed attempts before the account is locked (0 = never lock).</summary>
    [ConfigField(Label = "Max Failed Attempts", Min = 0, Max = 100, Group = "Lockout", Order = 20)]
    public int MaxFailedAttempts { get; set; } = 8;

    /// <summary>Lockout duration.</summary>
    [ConfigField(Label = "Lockout (min)", Min = 1, Max = 10080, Group = "Lockout", Order = 21)]
    public int LockoutMinutes { get; set; } = 15;

    /// <summary>Failures per login name before exponential back-off starts.</summary>
    [ConfigField(Label = "Free Attempts", Min = 1, Max = 100, Group = "Lockout", Order = 22, Collapsed = true)]
    public int FreeAttempts { get; set; } = 3;

    /// <summary>Longest back-off between attempts for one login name.</summary>
    [ConfigField(Label = "Max Back-off (s)", Min = 1, Max = 3600, Group = "Lockout", Order = 23, Collapsed = true)]
    public int MaxBackoffSeconds { get; set; } = 300;

    /// <summary>Failed sign-ins per client IP per hour before that IP is throttled.</summary>
    [ConfigField(Label = "Failures / IP / Hour", Min = 1, Max = 100000, Group = "Lockout", Order = 24, Collapsed = true)]
    public int MaxFailuresPerIpPerHour { get; set; } = 100;
}

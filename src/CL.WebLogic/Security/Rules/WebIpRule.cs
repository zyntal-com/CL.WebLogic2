namespace CL.WebLogic.Security.Rules;

/// <summary>
/// One app-managed IP rule (from an <see cref="IWebIpRuleProvider"/>, e.g. a bans table edited in
/// an admin UI). Set either <see cref="Network"/> or <see cref="Country"/>.
/// </summary>
public sealed record WebIpRule
{
    /// <summary>
    /// Stable id of the rule, reported in <c>WebSecurityRejectedEvent.RuleId</c> (e.g. a database
    /// key) so the app can count hits. Never put the client address here.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>Deny, allow or exempt.</summary>
    public WebIpRuleAction Action { get; init; } = WebIpRuleAction.Deny;

    /// <summary>An IP address or CIDR network (<c>203.0.113.7</c>, <c>2001:db8::/32</c>).</summary>
    public string? Network { get; init; }

    /// <summary>An ISO 3166 country code (needs CL.NetUtils GeoIP). Not valid with <see cref="WebIpRuleAction.Exempt"/>.</summary>
    public string? Country { get; init; }

    /// <summary>When the rule stops applying (null = never). Temporary bans need no clean-up job.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Why the rule exists (for the app's admin UI; logged at debug level).</summary>
    public string? Reason { get; init; }
}

namespace CL.WebLogic.Security.Rules;

/// <summary>
/// Drops the cached provider rules and DNSBL verdicts so the next request reloads them. Call it
/// after an admin adds or removes a ban, allow-list entry or blocklist zone.
/// </summary>
public interface IWebSecurityRulesInvalidator
{
    /// <summary>Reloads rules and zones on the next request.</summary>
    void Invalidate();
}

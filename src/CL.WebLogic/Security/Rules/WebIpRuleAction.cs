namespace CL.WebLogic.Security.Rules;

/// <summary>What an <see cref="WebIpRule"/> does with a matching client.</summary>
public enum WebIpRuleAction
{
    /// <summary>Refuse the client (403). Deny always wins.</summary>
    Deny,

    /// <summary>
    /// Allow-list entry: once any allow rule exists (config or provider), only clients matching
    /// one may connect.
    /// </summary>
    Allow,

    /// <summary>
    /// Trusted address (network rules only): passes the allow-list, country rules and the
    /// reputation check. Deny rules still apply.
    /// </summary>
    Exempt
}

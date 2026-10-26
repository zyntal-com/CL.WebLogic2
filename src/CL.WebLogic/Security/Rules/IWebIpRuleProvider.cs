namespace CL.WebLogic.Security.Rules;

/// <summary>
/// App-managed IP rules (bans, allow-lists, trusted addresses), merged with <c>Security.IpRules</c>.
/// Results are cached for <c>Security.IpRules.ProviderCacheSeconds</c>; call
/// <see cref="IWebSecurityRulesInvalidator.Invalidate"/> after editing them to apply at once. Register
/// with <c>web.AddIpRuleProvider&lt;T&gt;()</c>; several providers are combined.
/// </summary>
public interface IWebIpRuleProvider
{
    /// <summary>Returns the current rules. Expired rules may be included; they are ignored.</summary>
    ValueTask<IReadOnlyList<WebIpRule>> GetRulesAsync(CancellationToken cancellationToken);
}

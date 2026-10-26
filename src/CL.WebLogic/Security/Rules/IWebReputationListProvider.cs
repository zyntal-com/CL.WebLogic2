namespace CL.WebLogic.Security.Rules;

/// <summary>
/// App-managed DNS blocklist zones for the reputation stage. When the providers return any zones,
/// they replace the services configured in CL.NetUtils; an empty result keeps the CL.NetUtils
/// configuration. Cached like <see cref="IWebIpRuleProvider"/>. Register with
/// <c>web.AddReputationListProvider&lt;T&gt;()</c>.
/// </summary>
public interface IWebReputationListProvider
{
    /// <summary>Returns the zones to query.</summary>
    ValueTask<IReadOnlyList<WebReputationZone>> GetZonesAsync(CancellationToken cancellationToken);
}

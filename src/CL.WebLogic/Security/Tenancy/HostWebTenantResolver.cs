using CL.WebLogic.Security.Pipeline;

namespace CL.WebLogic.Security.Tenancy;

/// <summary>Resolves the tenant from the request host (exact host names, case-insensitive). Unknown hosts have no tenant.</summary>
public sealed class HostWebTenantResolver : IWebTenantResolver
{
    private readonly Dictionary<string, string> _tenants;

    /// <summary>Creates the resolver from host → tenant id.</summary>
    public HostWebTenantResolver(IReadOnlyDictionary<string, string> hostToTenant)
    {
        ArgumentNullException.ThrowIfNull(hostToTenant);
        _tenants = new Dictionary<string, string>(hostToTenant, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public ValueTask<string?> ResolveAsync(WebSecurityContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ValueTask.FromResult(_tenants.TryGetValue(context.HttpContext.Request.Host.Host, out var tenant) ? tenant : null);
    }
}

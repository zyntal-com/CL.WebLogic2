using CL.WebLogic.Security.Pipeline;

namespace CL.WebLogic.Security.Tenancy;

/// <summary>Resolves the tenant of a request (host, path prefix, header…). Optional; register one to enable tenancy.</summary>
public interface IWebTenantResolver
{
    /// <summary>The tenant id, or null when the request belongs to no tenant.</summary>
    ValueTask<string?> ResolveAsync(WebSecurityContext context, CancellationToken cancellationToken);
}

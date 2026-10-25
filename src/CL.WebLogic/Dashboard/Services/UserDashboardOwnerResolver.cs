using System.Security.Cryptography;
using System.Text;
using CL.WebLogic.Security.Pipeline;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Dashboard.Services;

/// <summary>Default owner: the signed-in user (per tenant). User ids are hashed so any id format yields a valid key.</summary>
public sealed class UserDashboardOwnerResolver : IWebDashboardOwnerResolver
{
    /// <inheritdoc/>
    public ValueTask<string?> ResolveAsync(HttpContext httpContext, string dashboardKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        var identity = WebSecurityContext.Get(httpContext)?.Identity;
        if (identity?.UserId is null)
            return ValueTask.FromResult<string?>(null);
        var source = (identity.TenantId ?? "") + "\n" + identity.UserId;
        return ValueTask.FromResult<string?>("user:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(source)))[..32]);
    }
}

using CL.WebLogic.Security.Identity;
using CL.WebLogic.Security.Pipeline;

namespace CL.WebLogic.Security.Authorization;

/// <summary>Resolves an identity's permissions once per request through <see cref="IWebPermissionResolver"/>.</summary>
public sealed class WebPermissionService
{
    private static readonly object CacheKey = new();
    private readonly IWebPermissionResolver _resolver;

    /// <summary>Creates the service.</summary>
    public WebPermissionService(IWebPermissionResolver resolver) =>
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));

    /// <summary>The permissions of the request's identity.</summary>
    public async ValueTask<IReadOnlyCollection<string>> GetAsync(WebSecurityContext security, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(security);
        if (Peek(security) is { } cached)
            return cached;
        if (!security.Identity.IsAuthenticated)
            return [];

        var resolved = await _resolver.ResolveAsync(security.Identity, cancellationToken).ConfigureAwait(false);
        var merged = security.Identity.Permissions.Concat(resolved).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        security.HttpContext.Items[CacheKey] = (security.Identity, (IReadOnlyCollection<string>)merged);
        return merged;
    }

    /// <summary>Permissions already resolved for this request (and identity), or null.</summary>
    public static IReadOnlyCollection<string>? Peek(WebSecurityContext security) =>
        security.HttpContext.Items.TryGetValue(CacheKey, out var value)
        && value is (WebIdentity identity, IReadOnlyCollection<string> permissions)
        && ReferenceEquals(identity, security.Identity)
            ? permissions
            : null;
}

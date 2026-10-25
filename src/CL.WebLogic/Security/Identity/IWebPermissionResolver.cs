namespace CL.WebLogic.Security.Identity;

/// <summary>
/// Resolves an identity's effective permissions (e.g. from its groups in the app's database).
/// Called lazily — only when a route, policy or template actually checks a permission — and
/// cached for the request.
/// </summary>
public interface IWebPermissionResolver
{
    /// <summary>The permissions of <paramref name="identity"/>.</summary>
    ValueTask<IReadOnlyCollection<string>> ResolveAsync(WebIdentity identity, CancellationToken cancellationToken);
}

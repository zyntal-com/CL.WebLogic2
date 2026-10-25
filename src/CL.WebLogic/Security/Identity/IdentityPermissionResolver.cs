namespace CL.WebLogic.Security.Identity;

/// <summary>Default resolver: the permissions carried by the identity itself.</summary>
public sealed class IdentityPermissionResolver : IWebPermissionResolver
{
    /// <inheritdoc/>
    public ValueTask<IReadOnlyCollection<string>> ResolveAsync(WebIdentity identity, CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyCollection<string>>(identity?.Permissions ?? []);
}

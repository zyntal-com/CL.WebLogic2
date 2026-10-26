using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Security.Pipeline;

namespace CL.WebLogic.Security.Authorization;

/// <summary>Default <see cref="IWebAuthorizationService"/>.</summary>
public sealed class WebAuthorizationService : IWebAuthorizationService
{
    private readonly Dictionary<string, WebAuthorizationPolicy> _policies;
    private readonly WebPermissionService _permissions;
    private readonly IWebLogicContext _context;
    private readonly TimeProvider _time;

    /// <summary>Creates the service from the registered policies (later registrations win).</summary>
    public WebAuthorizationService(IEnumerable<WebAuthorizationPolicy> policies, WebPermissionService permissions, IWebLogicContext context, TimeProvider? time = null)
    {
        _time = time ?? TimeProvider.System;
        _policies = new Dictionary<string, WebAuthorizationPolicy>(StringComparer.OrdinalIgnoreCase);
        foreach (var policy in policies ?? [])
            _policies[policy.Name] = policy;
        _permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>Registered policy names.</summary>
    public IReadOnlyCollection<string> PolicyNames => _policies.Keys;

    /// <inheritdoc/>
    public ValueTask<WebAuthorizationResult> AuthorizeAsync(WebSecurityContext security, string policyName, object? resource, CancellationToken cancellationToken)
    {
        if (!_policies.TryGetValue(policyName, out var policy))
        {
            _context.Logger.Error($"Unknown authorization policy '{policyName}' — access denied. Register it with AddAuthorizationPolicy.");
            return ValueTask.FromResult(WebAuthorizationResult.Forbidden("unknown-policy:" + policyName));
        }
        return AuthorizeAsync(security, policy.Requirements, resource, cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask<WebAuthorizationResult> AuthorizeAsync(WebSecurityContext security, IEnumerable<IWebAuthorizationRequirement> requirements, object? resource, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(security);
        var context = new WebAuthorizationContext(security, resource, _permissions, _time.GetUtcNow());
        foreach (var requirement in requirements)
        {
            if (await requirement.IsSatisfiedAsync(context, cancellationToken).ConfigureAwait(false))
                continue;
            if (!security.Identity.IsAuthenticated)
                return WebAuthorizationResult.NeedsAuthentication;
            return requirement is RecentSignInRequirement
                ? WebAuthorizationResult.NeedsReauthentication(requirement.Description)
                : WebAuthorizationResult.Forbidden(requirement.Description);
        }
        return WebAuthorizationResult.Success;
    }
}

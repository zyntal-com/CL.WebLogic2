using CL.WebLogic.Security.Pipeline;

namespace CL.WebLogic.Security.Authorization;

/// <summary>Evaluates policies and requirements — the same engine the authorization stage uses, for handler code.</summary>
public interface IWebAuthorizationService
{
    /// <summary>Evaluates a named policy (unknown names are denied).</summary>
    ValueTask<WebAuthorizationResult> AuthorizeAsync(WebSecurityContext security, string policyName, object? resource, CancellationToken cancellationToken);

    /// <summary>Evaluates requirements (all must hold).</summary>
    ValueTask<WebAuthorizationResult> AuthorizeAsync(WebSecurityContext security, IEnumerable<IWebAuthorizationRequirement> requirements, object? resource, CancellationToken cancellationToken);
}

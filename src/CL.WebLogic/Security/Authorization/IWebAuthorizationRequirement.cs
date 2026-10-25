namespace CL.WebLogic.Security.Authorization;

/// <summary>One condition of a policy. Implement it for custom or resource-based rules.</summary>
public interface IWebAuthorizationRequirement
{
    /// <summary>Short description (logged and shown in the explorer), e.g. <c>groups:admin</c>.</summary>
    string Description { get; }

    /// <summary>True when the requirement holds.</summary>
    ValueTask<bool> IsSatisfiedAsync(WebAuthorizationContext context, CancellationToken cancellationToken);
}

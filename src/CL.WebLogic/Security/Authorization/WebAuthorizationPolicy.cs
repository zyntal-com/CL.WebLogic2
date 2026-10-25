namespace CL.WebLogic.Security.Authorization;

/// <summary>A named set of requirements, all of which must hold. Referenced by routes with <c>.RequirePolicy("name")</c>.</summary>
public sealed class WebAuthorizationPolicy
{
    /// <summary>Creates the policy.</summary>
    public WebAuthorizationPolicy(string name, IReadOnlyList<IWebAuthorizationRequirement> requirements)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        Requirements = requirements ?? [];
    }

    /// <summary>Policy name.</summary>
    public string Name { get; }

    /// <summary>Requirements.</summary>
    public IReadOnlyList<IWebAuthorizationRequirement> Requirements { get; }
}

namespace CL.WebLogic.Security.Authorization;

/// <summary>Outcome of an authorization check.</summary>
public sealed class WebAuthorizationResult
{
    private WebAuthorizationResult(bool succeeded, bool challenge, string? failed)
    {
        Succeeded = succeeded;
        Challenge = challenge;
        FailedRequirement = failed;
    }

    /// <summary>Access granted.</summary>
    public static WebAuthorizationResult Success { get; } = new(true, false, null);

    /// <summary>Not signed in: ask for credentials (401 / login redirect).</summary>
    public static WebAuthorizationResult NeedsAuthentication { get; } = new(false, true, "authenticated");

    /// <summary>Signed in but not allowed (403).</summary>
    public static WebAuthorizationResult Forbidden(string failedRequirement) => new(false, false, failedRequirement);

    /// <summary>True when access is granted.</summary>
    public bool Succeeded { get; }

    /// <summary>True when the caller is unauthenticated.</summary>
    public bool Challenge { get; }

    /// <summary>The requirement that failed.</summary>
    public string? FailedRequirement { get; }
}

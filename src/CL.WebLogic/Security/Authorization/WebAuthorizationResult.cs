namespace CL.WebLogic.Security.Authorization;

/// <summary>Outcome of an authorization check.</summary>
public sealed class WebAuthorizationResult
{
    private WebAuthorizationResult(bool succeeded, bool challenge, bool reauthenticate, string? failed)
    {
        Succeeded = succeeded;
        Challenge = challenge;
        Reauthenticate = reauthenticate;
        FailedRequirement = failed;
    }

    /// <summary>Access granted.</summary>
    public static WebAuthorizationResult Success { get; } = new(true, false, false, null);

    /// <summary>Not signed in: ask for credentials (401 / login redirect).</summary>
    public static WebAuthorizationResult NeedsAuthentication { get; } = new(false, true, false, "authenticated");

    /// <summary>Signed in, but not recently enough (or not with the required methods): ask to sign in again.</summary>
    public static WebAuthorizationResult NeedsReauthentication(string failedRequirement) => new(false, false, true, failedRequirement);

    /// <summary>Signed in but not allowed (403).</summary>
    public static WebAuthorizationResult Forbidden(string failedRequirement) => new(false, false, false, failedRequirement);

    /// <summary>True when access is granted.</summary>
    public bool Succeeded { get; }

    /// <summary>True when the caller is unauthenticated.</summary>
    public bool Challenge { get; }

    /// <summary>True when the signed-in caller must prove their credentials again (step-up).</summary>
    public bool Reauthenticate { get; }

    /// <summary>The requirement that failed.</summary>
    public string? FailedRequirement { get; }
}

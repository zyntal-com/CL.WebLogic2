namespace CL.WebLogic.Security.Identity;

/// <summary>Result of an <see cref="IWebAuthenticationHandler"/>.</summary>
public sealed class WebAuthenticationResult
{
    private WebAuthenticationResult(WebIdentity? identity, string? failure)
    {
        Identity = identity;
        Failure = failure;
    }

    /// <summary>The handler found no credentials of its kind.</summary>
    public static WebAuthenticationResult NoResult { get; } = new(null, null);

    /// <summary>The credentials are valid.</summary>
    public static WebAuthenticationResult Success(WebIdentity identity) =>
        new(identity ?? throw new ArgumentNullException(nameof(identity)), null);

    /// <summary>Credentials were presented but are invalid (stops the request with 401).</summary>
    public static WebAuthenticationResult Fail(string reason) => new(null, reason);

    /// <summary>The identity on success.</summary>
    public WebIdentity? Identity { get; }

    /// <summary>The failure reason (internal), when failed.</summary>
    public string? Failure { get; }

    /// <summary>True on success.</summary>
    public bool Succeeded => Identity is not null;

    /// <summary>True when credentials were invalid.</summary>
    public bool Failed => Failure is not null;
}

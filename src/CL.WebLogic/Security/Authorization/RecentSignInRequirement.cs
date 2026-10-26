namespace CL.WebLogic.Security.Authorization;

/// <summary>
/// Step-up: the user proved their credentials within <see cref="MaxAge"/>, using every one of
/// <see cref="Methods"/> (e.g. <c>pwd</c> and <c>otp</c>). When it fails for a signed-in user the
/// result asks for re-authentication (<see cref="WebAuthorizationResult.Reauthenticate"/>) instead
/// of forbidding, so pages send the user to sign in again and APIs answer 401.
/// </summary>
public sealed class RecentSignInRequirement : IWebAuthorizationRequirement
{
    /// <summary>Creates the requirement.</summary>
    public RecentSignInRequirement(TimeSpan maxAge, IReadOnlyList<string>? methods = null)
    {
        if (maxAge <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(maxAge), "The maximum age must be positive.");
        MaxAge = maxAge;
        Methods = methods ?? [];
    }

    /// <summary>How long ago the credentials may have been proven.</summary>
    public TimeSpan MaxAge { get; }

    /// <summary>Methods that must all have been used (empty = any).</summary>
    public IReadOnlyList<string> Methods { get; }

    /// <inheritdoc/>
    public string Description => "recent-sign-in:" + (int)MaxAge.TotalSeconds + "s" + (Methods.Count == 0 ? "" : ":" + string.Join('+', Methods));

    /// <inheritdoc/>
    public ValueTask<bool> IsSatisfiedAsync(WebAuthorizationContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var identity = context.Identity;
        var recent = identity.AuthenticatedAt is { } at && context.Now - at <= MaxAge && at <= context.Now.AddMinutes(1);
        var methods = Methods.All(m => identity.AuthenticationMethods.Contains(m, StringComparer.OrdinalIgnoreCase));
        return ValueTask.FromResult(recent && methods);
    }
}

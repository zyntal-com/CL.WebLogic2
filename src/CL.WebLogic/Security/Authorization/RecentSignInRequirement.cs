namespace CL.WebLogic.Security.Authorization;

/// <summary>
/// Step-up: the user proved their credentials within <see cref="MaxAge"/>, and the sign-in used every
/// method of at least one of <see cref="Alternatives"/> — e.g. <c>[pwd, otp]</c> or <c>[passkey]</c>.
/// When it fails for a signed-in user the result asks for re-authentication
/// (<see cref="WebAuthorizationResult.Reauthenticate"/>) instead of forbidding, so pages send the user
/// to sign in again and APIs answer 401.
/// </summary>
public sealed class RecentSignInRequirement : IWebAuthorizationRequirement
{
    /// <summary>Creates the requirement: every one of <paramref name="methods"/> (none = any method).</summary>
    public RecentSignInRequirement(TimeSpan maxAge, IReadOnlyList<string>? methods = null)
        : this(maxAge, methods is { Count: > 0 } ? new[] { methods } : [])
    {
    }

    /// <summary>Creates the requirement: every method of at least one of <paramref name="alternatives"/> (none = any method).</summary>
    public RecentSignInRequirement(TimeSpan maxAge, IReadOnlyList<IReadOnlyList<string>> alternatives)
    {
        if (maxAge <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(maxAge), "The maximum age must be positive.");
        ArgumentNullException.ThrowIfNull(alternatives);
        if (alternatives.Any(a => a is null || a.Count == 0 || a.Any(string.IsNullOrWhiteSpace)))
            throw new ArgumentException("Each alternative needs at least one method name.", nameof(alternatives));
        MaxAge = maxAge;
        Alternatives = alternatives;
    }

    /// <summary>How long ago the credentials may have been proven.</summary>
    public TimeSpan MaxAge { get; }

    /// <summary>Sets of methods; the sign-in must have used all methods of one set (empty = any method).</summary>
    public IReadOnlyList<IReadOnlyList<string>> Alternatives { get; }

    /// <inheritdoc/>
    public string Description => "recent-sign-in:" + (int)MaxAge.TotalSeconds + "s"
        + (Alternatives.Count == 0 ? "" : ":" + string.Join('|', Alternatives.Select(a => string.Join('+', a))));

    /// <inheritdoc/>
    public ValueTask<bool> IsSatisfiedAsync(WebAuthorizationContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var identity = context.Identity;
        var recent = identity.AuthenticatedAt is { } at && context.Now - at <= MaxAge && at <= context.Now.AddMinutes(1);
        var methods = Alternatives.Count == 0
            || Alternatives.Any(set => set.All(m => identity.AuthenticationMethods.Contains(m, StringComparer.OrdinalIgnoreCase)));
        return ValueTask.FromResult(recent && methods);
    }
}

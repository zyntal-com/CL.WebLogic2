using CodeLogic.Core.Events;

namespace CL.WebLogic.Auth.Events;

/// <summary>A user signed in (<paramref name="Methods"/>: pwd, otp, recovery, link, external…).</summary>
public sealed record UserSignedInEvent(string UserId, string Scheme, IReadOnlyList<string> Methods) : IEvent
{
    /// <summary>
    /// True when the same user was signed in again without a new credential check (refresh,
    /// password change); false for a real sign-in or re-authentication.
    /// </summary>
    public bool IsRefresh { get; init; }
}

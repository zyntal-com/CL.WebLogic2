namespace CL.WebLogic.Auth.Users;

/// <summary>Why and until when an account is suspended (see <see cref="WebUser.SuspendedUntil"/>).</summary>
/// <param name="Until">When the suspension ends; <see cref="DateTimeOffset.MaxValue"/> = until lifted.</param>
/// <param name="Reason">The reason shown to the user, or null.</param>
public sealed record WebSuspension(DateTimeOffset Until, string? Reason)
{
    /// <summary>True when the suspension has no end date.</summary>
    public bool IsIndefinite => Until == DateTimeOffset.MaxValue;

    /// <summary>The suspension of <paramref name="user"/> at <paramref name="now"/>, or null when there is none.</summary>
    public static WebSuspension? Of(WebUser? user, DateTimeOffset now) =>
        user?.SuspendedUntil is { } until && until > now ? new WebSuspension(until, user.SuspensionReason) : null;
}

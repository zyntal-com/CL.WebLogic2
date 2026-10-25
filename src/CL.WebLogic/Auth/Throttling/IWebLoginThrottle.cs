using System.Net;

namespace CL.WebLogic.Auth.Throttling;

/// <summary>
/// Slows down password guessing: exponential back-off per login name and a cap on failures per
/// client IP. Separate from account lockout, so an attacker cannot lock users out from one IP
/// faster than the back-off allows.
/// </summary>
public interface IWebLoginThrottle
{
    /// <summary>How long to wait before another attempt for <paramref name="login"/> from <paramref name="ip"/>, or null when allowed.</summary>
    ValueTask<TimeSpan?> CheckAsync(string login, IPAddress? ip, CancellationToken cancellationToken);

    /// <summary>Records a failed attempt.</summary>
    ValueTask RecordFailureAsync(string login, IPAddress? ip, CancellationToken cancellationToken);

    /// <summary>Records a success (clears the login's back-off).</summary>
    ValueTask RecordSuccessAsync(string login, IPAddress? ip, CancellationToken cancellationToken);
}

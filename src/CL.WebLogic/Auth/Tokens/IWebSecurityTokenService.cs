using CL.WebLogic.Auth.Users;
using CodeLogic.Core.Results;

namespace CL.WebLogic.Auth.Tokens;

/// <summary>
/// Creates and validates account tokens: purpose-scoped, encrypted and signed with
/// DataProtection, time-limited, bound to the user's security stamp and (optionally) single-use.
/// </summary>
public interface IWebSecurityTokenService
{
    /// <summary>Creates a URL-safe token.</summary>
    string Create(string purpose, WebUser user, TimeSpan lifetime, string? data = null);

    /// <summary>
    /// Validates a token. With <paramref name="consume"/> the token is spent and later attempts
    /// fail. Every failure returns <c>weblogic.auth.token_invalid</c>.
    /// </summary>
    Task<Result<WebSecurityToken>> ValidateAsync(string purpose, string token, bool consume, CancellationToken cancellationToken);
}

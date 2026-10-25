using CL.WebLogic.Security.Pipeline;

namespace CL.WebLogic.Auth.SignIn;

/// <summary>Checks a login and password (throttling, lockout, hash upgrade) without signing in.</summary>
public interface IWebCredentialValidator
{
    /// <summary>Validates the credentials.</summary>
    Task<WebSignInResult> ValidateAsync(string login, string password, WebSecurityContext security, CancellationToken cancellationToken);
}

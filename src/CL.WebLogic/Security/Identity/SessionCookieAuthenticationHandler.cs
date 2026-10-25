using CL.WebLogic.Security.Pipeline;
using CL.WebLogic.Security.Sessions;

namespace CL.WebLogic.Security.Identity;

/// <summary>
/// Authenticates from the session cookie. Sessions of another tenant are ignored, and every
/// <see cref="IWebSessionValidator"/> must accept the session or it is signed out.
/// </summary>
public sealed class SessionCookieAuthenticationHandler : IWebAuthenticationHandler
{
    private readonly WebSessionManager _sessions;
    private readonly IWebSessionValidator[] _validators;

    /// <summary>Creates the handler.</summary>
    public SessionCookieAuthenticationHandler(WebSessionManager sessions, IEnumerable<IWebSessionValidator>? validators = null)
    {
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _validators = [.. validators ?? []];
    }

    /// <inheritdoc/>
    public string Scheme => WebAuthenticationSchemes.Session;

    /// <inheritdoc/>
    public int Order => 100;

    /// <inheritdoc/>
    public async ValueTask<WebAuthenticationResult> AuthenticateAsync(WebSecurityContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var session = await _sessions.GetAsync(context, cancellationToken).ConfigureAwait(false);
        if (session.AuthStage != WebSessionAuthStage.Authenticated || session.UserId is null)
            return WebAuthenticationResult.NoResult;
        if (context.TenantId is not null && session.TenantId is not null && !string.Equals(context.TenantId, session.TenantId, StringComparison.Ordinal))
            return WebAuthenticationResult.NoResult;

        foreach (var validator in _validators)
        {
            if (!await validator.ValidateAsync(session, context, cancellationToken).ConfigureAwait(false))
            {
                await _sessions.SignOutAsync(context, false, cancellationToken).ConfigureAwait(false);
                return WebAuthenticationResult.NoResult;
            }
        }
        return WebAuthenticationResult.Success(WebSessionManager.ToIdentity(session));
    }
}

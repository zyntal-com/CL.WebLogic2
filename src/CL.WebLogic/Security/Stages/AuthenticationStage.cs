using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Results;
using CL.WebLogic.Security.Identity;
using CL.WebLogic.Security.Pipeline;
using CL.WebLogic.Security.Sessions;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Security.Stages;

/// <summary>
/// Runs the authentication handlers in order and installs the identity (also as
/// <c>HttpContext.User</c>). Presented-but-invalid credentials stop the request with 401.
/// </summary>
public sealed class AuthenticationStage : IWebSecurityStage
{
    private readonly IWebAuthenticationHandler[] _handlers;
    private readonly WebSessionManager _sessions;

    /// <summary>Creates the stage.</summary>
    public AuthenticationStage(IEnumerable<IWebAuthenticationHandler> handlers, WebSessionManager sessions)
    {
        _handlers = [.. (handlers ?? []).OrderBy(h => h.Order)];
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
    }

    /// <inheritdoc/>
    public string Name => "authentication";

    /// <inheritdoc/>
    public WebSecurityPhase Phase => WebSecurityPhase.PreRouting;

    /// <inheritdoc/>
    public int Order => 500;

    /// <inheritdoc/>
    public async ValueTask<WebSecurityDecision> ExecuteAsync(WebSecurityContext context, CancellationToken cancellationToken)
    {
        // Every request gets its own session object (lazy: no store hit without a cookie).
        await _sessions.GetAsync(context, cancellationToken).ConfigureAwait(false);

        foreach (var handler in _handlers)
        {
            var result = await handler.AuthenticateAsync(context, cancellationToken).ConfigureAwait(false);
            if (result.Failed)
            {
                WebResult challenge = DefaultErrorPageRenderer.WantsProblemJson(context.HttpContext)
                    ? WebResult.Problem(StatusCodes.Status401Unauthorized, detail: "The supplied credentials are not valid.")
                    : WebResult.Status(StatusCodes.Status401Unauthorized);
                challenge.WithHeader("WWW-Authenticate", handler.Scheme == WebAuthenticationSchemes.Bearer ? "Bearer error=\"invalid_token\"" : "WebLogic");
                return WebSecurityDecision.ShortCircuit(challenge, "invalid-credentials:" + handler.Scheme);
            }

            if (result.Succeeded)
            {
                context.Identity = result.Identity!;
                context.HttpContext.User = result.Identity!.Principal;
                break;
            }
        }

        return WebSecurityDecision.Continue;
    }
}

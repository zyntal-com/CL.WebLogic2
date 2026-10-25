using System.Text.Encodings.Web;
using CL.WebLogic.Security.Pipeline;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CL.WebLogic.Security.Services;

/// <summary>
/// ASP.NET Core authentication scheme <c>WebLogic</c> that replays the identity the security
/// pipeline established, so <c>[Authorize]</c> / <c>RequireAuthorization()</c> on minimal APIs work.
/// </summary>
public sealed class WebLogicAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    /// <summary>The scheme name.</summary>
    public const string SchemeName = "WebLogic";

    /// <summary>Creates the handler.</summary>
    public WebLogicAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    /// <inheritdoc/>
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = WebSecurityContext.Get(Context)?.Identity;
        return Task.FromResult(identity is { IsAuthenticated: true }
            ? AuthenticateResult.Success(new AuthenticationTicket(identity.Principal, SchemeName))
            : AuthenticateResult.NoResult());
    }
}

using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Results;
using CL.WebLogic.Routing;
using CL.WebLogic.Routing.Models;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Security.Antiforgery;

namespace CL.WebLogic.Security.Services;

/// <summary>Built-in security routes: <c>GET /_weblogic/csrf</c> hands a token to SPAs and API-style clients.</summary>
public sealed class SecurityContributor : IWebContributor
{
    /// <summary>Descriptor of the built-in security routes.</summary>
    public static WebContributorDescriptor Descriptor { get; } = new()
    {
        Id = "weblogic.security",
        Name = "WebLogic security",
        Kind = "System"
    };

    private readonly IWebCsrfTokens _tokens;
    private readonly WebLogicConfigHolder _config;

    /// <summary>Creates the contributor.</summary>
    public SecurityContributor(IWebCsrfTokens tokens, WebLogicConfigHolder config)
    {
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <inheritdoc/>
    public Task ContributeAsync(WebContributionBuilder builder, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.MapApi("/_weblogic/csrf", (request, _) =>
        {
            var csrf = _config.Current.Security.Csrf;
            return ValueTask.FromResult<WebResult>(WebResult.Json(new
            {
                Token = _tokens.GetToken(request.HttpContext),
                csrf.HeaderName,
                csrf.FieldName
            }));
        })
        .WithName("weblogic.csrf")
        .WithDescription("Issues a CSRF token for the current browser.")
        .AllowAnonymous();
        return Task.CompletedTask;
    }
}

using System.Security.Claims;
using CL.WebLogic.Security.Identity;
using CL.WebLogic.Security.Pipeline;
using Microsoft.AspNetCore.Authentication;

namespace CL.WebLogic.Auth.Handlers;

/// <summary>
/// Bridges an ASP.NET authentication scheme the app added (Negotiate, JwtBearer, certificate…)
/// into WebLogic: the scheme's principal becomes the <see cref="WebIdentity"/>. Registered by
/// <c>web.AddAspNetAuthenticationScheme(name)</c>.
/// </summary>
public sealed class AspNetSchemeAuthenticationHandler : IWebAuthenticationHandler
{
    private readonly string _scheme;
    private readonly bool _ambient;

    /// <summary>Creates the handler for <paramref name="scheme"/>. <paramref name="ambient"/>: browsers send the credential automatically (Negotiate, client certificates) so CSRF applies.</summary>
    public AspNetSchemeAuthenticationHandler(string scheme, bool ambient, int order = 400)
    {
        ArgumentException.ThrowIfNullOrEmpty(scheme);
        _scheme = scheme;
        _ambient = ambient;
        Order = order;
    }

    /// <inheritdoc/>
    public string Scheme => WebAuthenticationSchemes.AspNet + ":" + _scheme;

    /// <inheritdoc/>
    public int Order { get; }

    /// <inheritdoc/>
    public async ValueTask<WebAuthenticationResult> AuthenticateAsync(WebSecurityContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var result = await context.HttpContext.AuthenticateAsync(_scheme).ConfigureAwait(false);
        if (result.None || !result.Succeeded)
            return result.Failure is null || result.None ? WebAuthenticationResult.NoResult : WebAuthenticationResult.Fail("aspnet-" + _scheme);

        var principal = result.Principal!;
        var id = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub") ?? principal.Identity?.Name;
        if (id is null)
            return WebAuthenticationResult.Fail("aspnet-no-subject");

        return WebAuthenticationResult.Success(new WebIdentity
        {
            UserId = id,
            UserName = principal.Identity?.Name ?? id,
            Scheme = Scheme,
            IsAmbient = _ambient,
            Groups = [.. principal.FindAll(ClaimTypes.Role).Select(c => c.Value)],
            Scopes = [.. principal.FindAll("scope").SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))],
            TenantId = principal.FindFirstValue("tid") ?? context.TenantId,
            AuthenticationMethods = [_scheme.ToLowerInvariant()]
        });
    }
}

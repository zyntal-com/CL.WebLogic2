using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Realtime.Models;
using CL.WebLogic.Security.Authorization;
using CL.WebLogic.Security.Identity;
using CL.WebLogic.Security.Pipeline;
using CL.WebLogic.Security.Sessions;

namespace CL.WebLogic.Realtime.Services;

/// <summary>Captures what a request's caller may receive (groups, resolved permissions, Internal membership).</summary>
public sealed class WebRealtimeIdentityFactory
{
    private readonly WebPermissionService _permissions;
    private readonly WebLogicConfigHolder _config;

    /// <summary>Creates the factory.</summary>
    public WebRealtimeIdentityFactory(WebPermissionService permissions, WebLogicConfigHolder config)
    {
        _permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>The caller's realtime identity.</summary>
    public async ValueTask<WebRealtimeIdentity> CreateAsync(WebSecurityContext? security, CancellationToken cancellationToken)
    {
        if (security is null || !security.Identity.IsAuthenticated)
            return WebRealtimeIdentity.Anonymous;
        var permissions = (await _permissions.GetAsync(security, cancellationToken).ConfigureAwait(false)).ToList();
        var identity = security.Identity;
        return new WebRealtimeIdentity
        {
            UserId = identity.UserId,
            SessionId = SessionIdHash(security),
            ApiKeyId = identity.Scheme == WebAuthenticationSchemes.ApiKey && identity.Claims.TryGetValue(WebIdentityClaims.KeyId, out var keyId) ? keyId : null,
            TenantId = identity.TenantId ?? security.TenantId,
            Groups = identity.Groups,
            Permissions = permissions,
            IsInternal = WebPermissionMatcher.Grants(permissions, _config.Current.Realtime.InternalPermission)
        };
    }

    // The session behind a session-scheme identity, so the connection can end with it.
    private static string? SessionIdHash(WebSecurityContext security) =>
        security.Identity.Scheme == WebAuthenticationSchemes.Session
        && security.HttpContext.Features.Get<IWebSession>() is LazyWebSession { AuthStage: WebSessionAuthStage.Authenticated } session
        && string.Equals(session.UserId, security.Identity.UserId, StringComparison.Ordinal)
            ? session.Loaded?.IdHash
            : null;
}

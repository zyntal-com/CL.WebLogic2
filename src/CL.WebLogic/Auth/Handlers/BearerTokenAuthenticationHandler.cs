using CL.WebLogic.Auth.ApiKeys;
using CL.WebLogic.Security.Identity;
using CL.WebLogic.Security.Pipeline;

namespace CL.WebLogic.Auth.Handlers;

/// <summary>
/// Authenticates <c>Authorization: Bearer …</c> through the app's
/// <see cref="IWebBearerTokenValidator"/> (WebLogic API keys are left to the API key handler).
/// Registered by <c>web.AddBearerTokens&lt;T&gt;()</c>.
/// </summary>
public sealed class BearerTokenAuthenticationHandler : IWebAuthenticationHandler
{
    private readonly IWebBearerTokenValidator _validator;

    /// <summary>Creates the handler.</summary>
    public BearerTokenAuthenticationHandler(IWebBearerTokenValidator validator) =>
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));

    /// <inheritdoc/>
    public string Scheme => WebAuthenticationSchemes.Bearer;

    /// <inheritdoc/>
    public int Order => 300;

    /// <inheritdoc/>
    public async ValueTask<WebAuthenticationResult> AuthenticateAsync(WebSecurityContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var authorization = context.HttpContext.Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return WebAuthenticationResult.NoResult;

        var token = authorization[7..].Trim();
        if (token.Length == 0 || WebApiKeyFormat.LooksLikeKey(token))
            return WebAuthenticationResult.NoResult;

        var identity = await _validator.ValidateAsync(token, context, cancellationToken).ConfigureAwait(false);
        if (identity is null || !identity.IsAuthenticated)
            return WebAuthenticationResult.Fail("bearer-invalid");

        // Bearer tokens are never sent automatically by browsers, so no CSRF exposure.
        return WebAuthenticationResult.Success(new WebIdentity
        {
            UserId = identity.UserId,
            UserName = identity.UserName,
            Scheme = WebAuthenticationSchemes.Bearer,
            IsAmbient = false,
            Groups = identity.Groups,
            Permissions = identity.Permissions,
            Scopes = identity.Scopes,
            TenantId = identity.TenantId,
            AuthenticationMethods = identity.AuthenticationMethods.Count > 0 ? identity.AuthenticationMethods : ["bearer"],
            Claims = identity.Claims
        });
    }
}

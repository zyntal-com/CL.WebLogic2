using System.Security.Cryptography;
using System.Text;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Security.Identity;
using CL.WebLogic.Security.Pipeline;

namespace CL.WebLogic.Auth.Handlers;

/// <summary>
/// Accepts the identity an authenticating reverse proxy puts in headers — only when the direct
/// peer is a trusted proxy and the shared secret matches. Anything else is ignored (headers from
/// untrusted peers are never believed).
/// </summary>
public sealed class TrustedHeaderAuthenticationHandler : IWebAuthenticationHandler
{
    private readonly WebLogicConfigHolder _config;
    private readonly IWebLogicContext _context;

    /// <summary>Creates the handler.</summary>
    public TrustedHeaderAuthenticationHandler(WebLogicConfigHolder config, IWebLogicContext context)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc/>
    public string Scheme => WebAuthenticationSchemes.TrustedHeader;

    /// <inheritdoc/>
    public int Order => 50;

    /// <inheritdoc/>
    public ValueTask<WebAuthenticationResult> AuthenticateAsync(WebSecurityContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var settings = _config.Current.Auth.TrustedHeader;
        if (!settings.Enabled)
            return ValueTask.FromResult(WebAuthenticationResult.NoResult);

        var headers = context.HttpContext.Request.Headers;
        var userId = headers[settings.UserHeader].ToString().Trim();
        if (userId.Length == 0)
            return ValueTask.FromResult(WebAuthenticationResult.NoResult);

        if (!context.PeerIsTrustedProxy)
        {
            _context.Logger.Warning($"Ignored {settings.UserHeader} from untrusted peer {context.PeerIp}.");
            return ValueTask.FromResult(WebAuthenticationResult.NoResult);
        }

        var secret = Encoding.UTF8.GetBytes(headers[settings.SecretHeader].ToString());
        var expected = Encoding.UTF8.GetBytes(settings.Secret);
        if (expected.Length == 0 || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(secret), SHA256.HashData(expected)))
            return ValueTask.FromResult(WebAuthenticationResult.Fail("trusted-header-secret"));

        var email = headers[settings.EmailHeader].ToString().Trim();
        var groups = headers[settings.GroupsHeader].ToString()
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var name = headers[settings.NameHeader].ToString().Trim();

        return ValueTask.FromResult(WebAuthenticationResult.Success(new WebIdentity
        {
            UserId = userId,
            UserName = name.Length > 0 ? name : userId,
            Scheme = WebAuthenticationSchemes.TrustedHeader,
            IsAmbient = true, // the browser sends the proxy's cookie automatically: CSRF applies
            Groups = groups,
            TenantId = context.TenantId,
            AuthenticationMethods = ["proxy"],
            Claims = email.Length > 0 ? new Dictionary<string, string> { [WebIdentityClaims.Email] = email } : new Dictionary<string, string>()
        }));
    }
}

using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Security.Pipeline;
using CL.WebLogic.Security.Sessions;
using CL.WebLogic.Templates.Rendering;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Security.Antiforgery;

/// <summary>
/// Synchronizer tokens bound to the session's secret — or, before a session exists (login and
/// sign-up forms), to a secret in a DataProtection-protected pre-session cookie. Every rendered
/// token is freshly masked (BREACH mitigation). A new session means a new secret, so tokens
/// rotate on sign-in and sign-out.
/// </summary>
public sealed class WebCsrfTokens : IWebCsrfTokens, ITemplateCsrfProvider
{
    private const int SecretBytes = 32;
    private readonly IDataProtector _protector;
    private readonly WebLogicConfigHolder _config;

    /// <summary>Creates the service.</summary>
    public WebCsrfTokens(IDataProtectionProvider dataProtection, WebLogicConfigHolder config)
    {
        _protector = (dataProtection ?? throw new ArgumentNullException(nameof(dataProtection))).CreateProtector("CL.WebLogic.Csrf.PreSession.v1");
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <inheritdoc/>
    public string FieldName => _config.Current.Security.Csrf.FieldName;

    /// <inheritdoc/>
    public string GetToken(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        var secret = SessionSecret(httpContext) ?? PreSessionSecret(httpContext, create: true);
        if (secret is null)
            return string.Empty;

        if (WebSecurityContext.Get(httpContext) is { } security)
            security.CsrfTokenIssued = true;
        return Mask(secret);
    }

    /// <inheritdoc/>
    public bool Validate(HttpContext httpContext, string? token)
    {
        if (Unmask(token) is not { } candidate)
            return false;
        return Matches(SessionSecret(httpContext), candidate) || Matches(PreSessionSecret(httpContext, create: false), candidate);
    }

    private static byte[]? SessionSecret(HttpContext httpContext) =>
        httpContext.Features.Get<IWebSession>() is LazyWebSession { IsPersisted: true, CsrfSecret: { } secret } ? Decode(secret) : null;

    private byte[]? PreSessionSecret(HttpContext httpContext, bool create)
    {
        var cookieName = WebSessionCookies.Name(httpContext, _config.Current.Security.Csrf.PreSessionCookieName);
        if (httpContext.Items.TryGetValue(this, out var issued) && issued is byte[] pending)
            return pending;

        if (httpContext.Request.Cookies[cookieName] is { Length: > 0 } protectedValue)
        {
            try
            {
                return _protector.Unprotect(Base64Url.DecodeFromChars(protectedValue));
            }
            catch (Exception ex) when (ex is CryptographicException or FormatException)
            {
                // Tampered or from an old key ring: fall through and issue a new one.
            }
        }

        if (!create || httpContext.Response.HasStarted)
            return null;

        var secret = RandomNumberGenerator.GetBytes(SecretBytes);
        httpContext.Items[this] = secret;
        httpContext.Response.Cookies.Append(cookieName, Base64Url.EncodeToString(_protector.Protect(secret)), WebSessionCookies.Options(httpContext, null));
        return secret;
    }

    private static string Mask(byte[] secret)
    {
        var token = new byte[secret.Length * 2];
        RandomNumberGenerator.Fill(token.AsSpan(0, secret.Length));
        for (var i = 0; i < secret.Length; i++)
            token[secret.Length + i] = (byte)(secret[i] ^ token[i]);
        return Base64Url.EncodeToString(token);
    }

    private static byte[]? Unmask(string? token)
    {
        if (string.IsNullOrEmpty(token) || token.Length > 128)
            return null;
        byte[] raw;
        try
        {
            raw = Base64Url.DecodeFromChars(token);
        }
        catch (FormatException)
        {
            return null;
        }
        if (raw.Length != SecretBytes * 2)
            return null;
        var secret = new byte[SecretBytes];
        for (var i = 0; i < SecretBytes; i++)
            secret[i] = (byte)(raw[SecretBytes + i] ^ raw[i]);
        return secret;
    }

    private static bool Matches(byte[]? expected, byte[] candidate) =>
        expected is not null && CryptographicOperations.FixedTimeEquals(expected, candidate);

    private static byte[]? Decode(string secret)
    {
        try
        {
            return Base64Url.DecodeFromChars(secret);
        }
        catch (FormatException)
        {
            return Encoding.UTF8.GetBytes(secret);
        }
    }
}

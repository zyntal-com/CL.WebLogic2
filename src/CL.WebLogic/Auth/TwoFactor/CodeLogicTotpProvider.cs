using CL.TwoFactorAuth;
using CL.TwoFactorAuth.Models;
using CL.TwoFactorAuth.Services;
using CL.WebLogic.Hosting.Services;

namespace CL.WebLogic.Auth.TwoFactor;

/// <summary>
/// TOTP via CL.TwoFactorAuth: the loaded <see cref="TwoFactorAuthLibrary"/> when present (its
/// configuration applies), otherwise its services with default settings (30 s steps, ±1 step).
/// </summary>
public sealed class CodeLogicTotpProvider : IWebTotpProvider
{
    private readonly IWebLibraryResolver _libraries;
    private readonly IWebLogicContext _context;
    private readonly Lazy<(TwoFactorAuthenticator Authenticator, QrCodeGenerator QrCode)> _fallback;

    /// <summary>Creates the provider.</summary>
    public CodeLogicTotpProvider(IWebLibraryResolver libraries, IWebLogicContext context)
    {
        _libraries = libraries ?? throw new ArgumentNullException(nameof(libraries));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _fallback = new(() =>
        {
            var config = new TwoFactorAuthConfig();
            return (new TwoFactorAuthenticator(config, _context.Logger), new QrCodeGenerator(config, _context.Logger));
        });
    }

    /// <inheritdoc/>
    public string GenerateSecret() => Services().Authenticator.GenerateSecretKey();

    /// <inheritdoc/>
    public string GetProvisioningUri(string secret, string issuer, string account) =>
        new TwoFactorKey(secret, issuer, account).ProvisioningUri;

    /// <inheritdoc/>
    public string? GetQrCodeDataUri(string secret, string issuer, string account)
    {
        var result = Services().QrCode.GenerateQrCodeDataUri(new TwoFactorKey(secret, issuer, account));
        return result.IsSuccess ? result.Value : null;
    }

    /// <inheritdoc/>
    public long? Validate(string code, string secret)
    {
        var digits = new string((code ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        if (digits.Length != 6 || string.IsNullOrEmpty(secret))
            return null;

        var authenticator = Services().Authenticator;
        var result = authenticator.ValidateTotp(digits, secret);
        if (!result.IsValid)
            return null;

        // CL.TwoFactorAuth reports the matched step; tolerate a relative offset as well.
        var matched = result.MatchedWindow ?? 0;
        return Math.Abs(matched) < 1000 ? authenticator.GetCurrentTimeWindow() + matched : matched;
    }

    private (TwoFactorAuthenticator Authenticator, QrCodeGenerator QrCode) Services()
    {
        var library = _libraries.GetLibrary<TwoFactorAuthLibrary>();
        if (library is not null)
        {
            try
            {
                return (library.Authenticator, library.QrCode);
            }
            catch (InvalidOperationException)
            {
                // Loaded but disabled or not initialized: use defaults.
            }
        }
        return _fallback.Value;
    }
}

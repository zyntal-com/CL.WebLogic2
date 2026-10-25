namespace CL.WebLogic.Auth.TwoFactor;

/// <summary>TOTP primitives (RFC 6238). The default uses CL.TwoFactorAuth.</summary>
public interface IWebTotpProvider
{
    /// <summary>A new Base32 secret.</summary>
    string GenerateSecret();

    /// <summary>The <c>otpauth://</c> URI for authenticator apps.</summary>
    string GetProvisioningUri(string secret, string issuer, string account);

    /// <summary>A QR code (PNG data URI) of the provisioning URI, or null when unavailable.</summary>
    string? GetQrCodeDataUri(string secret, string issuer, string account);

    /// <summary>Checks <paramref name="code"/>; returns the matched time step (for replay protection) or null.</summary>
    long? Validate(string code, string secret);
}

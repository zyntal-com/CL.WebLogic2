namespace CL.WebLogic.Auth.TwoFactor;

/// <summary>What the enrollment page shows: the secret (for manual entry), the QR code and a token to confirm with.</summary>
public sealed class WebTwoFactorEnrollment
{
    /// <summary>Base32 secret for manual entry.</summary>
    public required string Secret { get; init; }

    /// <summary>The <c>otpauth://</c> URI.</summary>
    public required string ProvisioningUri { get; init; }

    /// <summary>QR code as a PNG data URI (null when it could not be generated).</summary>
    public string? QrCodeDataUri { get; init; }

    /// <summary>Opaque token to send back with the first code (keeps the secret off the session).</summary>
    public required string EnrollmentToken { get; init; }
}

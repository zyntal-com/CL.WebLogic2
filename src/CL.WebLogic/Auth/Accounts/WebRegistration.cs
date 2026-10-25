namespace CL.WebLogic.Auth.Accounts;

/// <summary>A new account.</summary>
public sealed class WebRegistration
{
    /// <summary>Login name.</summary>
    public required string UserName { get; init; }

    /// <summary>E-mail address.</summary>
    public string? Email { get; init; }

    /// <summary>Password (null for password-less accounts: magic links or external login only).</summary>
    public string? Password { get; init; }

    /// <summary>Initial groups.</summary>
    public IReadOnlyList<string> Groups { get; init; } = [];

    /// <summary>Initial permissions.</summary>
    public IReadOnlyList<string> Permissions { get; init; } = [];

    /// <summary>Tenant.</summary>
    public string? TenantId { get; init; }

    /// <summary>Mark the e-mail as confirmed (e.g. verified by an external provider).</summary>
    public bool EmailConfirmed { get; init; }

    /// <summary>Send the verification e-mail (when there is an unconfirmed address).</summary>
    public bool SendVerificationEmail { get; init; } = true;
}

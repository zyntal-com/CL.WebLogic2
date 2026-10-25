using CL.WebLogic.Auth.Users;

namespace CL.WebLogic.Auth.Tokens;

/// <summary>A validated account token.</summary>
public sealed class WebSecurityToken
{
    /// <summary>The purpose.</summary>
    public required string Purpose { get; init; }

    /// <summary>The user the token was made for (current state from the store).</summary>
    public required WebUser User { get; init; }

    /// <summary>Extra data carried by the token (new e-mail address, 2FA secret…).</summary>
    public string? Data { get; init; }

    /// <summary>Expiry.</summary>
    public required DateTimeOffset ExpiresAt { get; init; }
}

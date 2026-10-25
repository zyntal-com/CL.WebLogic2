namespace CL.WebLogic.Auth.External;

/// <summary>Who signed in at the provider.</summary>
public sealed class WebExternalLoginInfo
{
    /// <summary>Provider name.</summary>
    public required string Provider { get; init; }

    /// <summary>The user's stable id at the provider.</summary>
    public required string ProviderKey { get; init; }

    /// <summary>Display name, when known.</summary>
    public string? UserName { get; init; }

    /// <summary>E-mail, when known.</summary>
    public string? Email { get; init; }

    /// <summary>True when the provider verified the e-mail.</summary>
    public bool EmailVerified { get; init; }

    /// <summary>Extra values (avatar URL…).</summary>
    public IReadOnlyDictionary<string, string> Values { get; init; } = new Dictionary<string, string>();
}

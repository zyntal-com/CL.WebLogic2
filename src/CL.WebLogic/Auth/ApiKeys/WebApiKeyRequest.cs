namespace CL.WebLogic.Auth.ApiKeys;

/// <summary>What to create an API key with.</summary>
public sealed class WebApiKeyRequest
{
    /// <summary>Display name.</summary>
    public required string Name { get; init; }

    /// <summary>Owning user.</summary>
    public string? UserId { get; init; }

    /// <summary>Scopes.</summary>
    public IReadOnlyList<string> Scopes { get; init; } = [];

    /// <summary>Groups.</summary>
    public IReadOnlyList<string> Groups { get; init; } = [];

    /// <summary>Permissions.</summary>
    public IReadOnlyList<string> Permissions { get; init; } = [];

    /// <summary>Tenant.</summary>
    public string? TenantId { get; init; }

    /// <summary>Allowed networks.</summary>
    public IReadOnlyList<string> AllowedNetworks { get; init; } = [];

    /// <summary>Rate-limit policy.</summary>
    public string? RateLimitPolicy { get; init; }

    /// <summary>Lifetime; null = no expiry.</summary>
    public TimeSpan? Lifetime { get; init; }
}

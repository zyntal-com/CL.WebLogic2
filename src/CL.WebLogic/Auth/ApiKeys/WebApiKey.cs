namespace CL.WebLogic.Auth.ApiKeys;

/// <summary>A stored API key. Only the SHA-256 hash of the secret is kept.</summary>
public sealed record WebApiKey
{
    /// <summary>Stable id.</summary>
    public required string Id { get; init; }

    /// <summary>The public part (<c>wl_{prefix}_…</c>) used to find the key.</summary>
    public required string Prefix { get; init; }

    /// <summary>Lower-case hex SHA-256 of the full key.</summary>
    public required string SecretHash { get; init; }

    /// <summary>Display name.</summary>
    public required string Name { get; init; }

    /// <summary>Owning user, when the key acts for a user.</summary>
    public string? UserId { get; init; }

    /// <summary>Scopes (checked by <c>RequireScopes</c>).</summary>
    public IReadOnlyList<string> Scopes { get; init; } = [];

    /// <summary>Groups granted to requests with this key.</summary>
    public IReadOnlyList<string> Groups { get; init; } = [];

    /// <summary>Permissions granted to requests with this key.</summary>
    public IReadOnlyList<string> Permissions { get; init; } = [];

    /// <summary>Tenant the key belongs to.</summary>
    public string? TenantId { get; init; }

    /// <summary>Networks the key may be used from (CIDR or address); empty = anywhere.</summary>
    public IReadOnlyList<string> AllowedNetworks { get; init; } = [];

    /// <summary>Rate-limit policy applied to every request with this key.</summary>
    public string? RateLimitPolicy { get; init; }

    /// <summary>Creation time.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Expiry, when set.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Last use (updated at most every <c>Auth.ApiKeys.TouchIntervalMinutes</c>).</summary>
    public DateTimeOffset? LastUsedAt { get; init; }

    /// <summary>Revocation time; revoked keys never authenticate.</summary>
    public DateTimeOffset? RevokedAt { get; init; }
}

namespace CL.WebLogic.Security.Audit;

/// <summary>One audit record. Client addresses are HMAC hashes, never raw IPs.</summary>
public sealed record WebAuditEntry
{
    /// <summary>When it happened (UTC).</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Category, e.g. <c>security.antiforgery</c>, <c>auth.sign-in</c>, <c>request</c>.</summary>
    public required string Category { get; init; }

    /// <summary>Outcome / reason, e.g. <c>missing-token</c>, <c>succeeded</c>.</summary>
    public required string Outcome { get; init; }

    /// <summary>HTTP status of the response (0 when not tied to a response).</summary>
    public int StatusCode { get; init; }

    /// <summary>User id, if authenticated.</summary>
    public string? UserId { get; init; }

    /// <summary>Tenant, if any.</summary>
    public string? TenantId { get; init; }

    /// <summary>HMAC-SHA256 of the client IP.</summary>
    public string? IpHash { get; init; }

    /// <summary>Request method.</summary>
    public string Method { get; init; } = string.Empty;

    /// <summary>Request path (no query string).</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>Correlation id of the request.</summary>
    public string TraceId { get; init; } = string.Empty;

    /// <summary>Extra data (never secrets).</summary>
    public IReadOnlyDictionary<string, string> Data { get; init; } = new Dictionary<string, string>();
}

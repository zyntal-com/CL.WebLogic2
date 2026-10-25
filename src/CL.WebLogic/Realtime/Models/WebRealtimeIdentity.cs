namespace CL.WebLogic.Realtime.Models;

/// <summary>What a connection is allowed to receive, captured when it opens.</summary>
public sealed record WebRealtimeIdentity
{
    /// <summary>Anonymous.</summary>
    public static WebRealtimeIdentity Anonymous { get; } = new();

    /// <summary>User id (null = anonymous).</summary>
    public string? UserId { get; init; }

    /// <summary>Session id (null for tickets and non-session schemes).</summary>
    public string? SessionId { get; init; }

    /// <summary>Tenant.</summary>
    public string? TenantId { get; init; }

    /// <summary>Groups.</summary>
    public IReadOnlyList<string> Groups { get; init; } = [];

    /// <summary>Resolved permissions.</summary>
    public IReadOnlyList<string> Permissions { get; init; } = [];

    /// <summary>Member of the Internal audience.</summary>
    public bool IsInternal { get; init; }

    /// <summary>True when signed in.</summary>
    public bool IsAuthenticated => UserId is not null;
}

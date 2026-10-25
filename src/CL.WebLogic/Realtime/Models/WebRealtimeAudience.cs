namespace CL.WebLogic.Realtime.Models;

/// <summary>The audience of a realtime message.</summary>
public sealed record WebRealtimeAudience(WebRealtimeAudienceKind Kind, string? Value = null)
{
    /// <summary>Everyone.</summary>
    public static WebRealtimeAudience Public { get; } = new(WebRealtimeAudienceKind.Public);

    /// <summary>Signed-in users.</summary>
    public static WebRealtimeAudience Authenticated { get; } = new(WebRealtimeAudienceKind.Authenticated);

    /// <summary>Operators.</summary>
    public static WebRealtimeAudience Internal { get; } = new(WebRealtimeAudienceKind.Internal);

    /// <summary>Members of <paramref name="group"/>.</summary>
    public static WebRealtimeAudience Group(string group) => new(WebRealtimeAudienceKind.Group, Required(group));

    /// <summary>Holders of <paramref name="permission"/>.</summary>
    public static WebRealtimeAudience Permission(string permission) => new(WebRealtimeAudienceKind.Permission, Required(permission));

    /// <summary>One user.</summary>
    public static WebRealtimeAudience User(string userId) => new(WebRealtimeAudienceKind.User, Required(userId));

    /// <summary>One tenant.</summary>
    public static WebRealtimeAudience Tenant(string tenantId) => new(WebRealtimeAudienceKind.Tenant, Required(tenantId));

    private static string Required(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);
        return value;
    }
}

namespace CL.WebLogic.Auth.Notifications;

/// <summary>A security e-mail to send.</summary>
public sealed record WebSecurityNotification
{
    /// <summary>The kind.</summary>
    public required WebSecurityNotificationKind Kind { get; init; }

    /// <summary>Recipient address.</summary>
    public required string ToAddress { get; init; }

    /// <summary>User id.</summary>
    public required string UserId { get; init; }

    /// <summary>User name (for the greeting).</summary>
    public required string UserName { get; init; }

    /// <summary>The action link, for kinds that have one. Never log it.</summary>
    public string? Link { get; init; }

    /// <summary>Extra values for templates.</summary>
    public IReadOnlyDictionary<string, string> Values { get; init; } = new Dictionary<string, string>();
}

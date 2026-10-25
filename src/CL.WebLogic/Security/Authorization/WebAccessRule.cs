namespace CL.WebLogic.Security.Authorization;

/// <summary>
/// Who may see or use something that is not a route (a widget, a widget placement, an options
/// provider…). Secure by default: signed-in users only unless <see cref="AllowAnonymous"/>.
/// </summary>
public sealed record WebAccessRule
{
    /// <summary>Signed-in users only (the default).</summary>
    public static WebAccessRule Authenticated { get; } = new();

    /// <summary>Everyone.</summary>
    public static WebAccessRule Anonymous { get; } = new() { AllowAnonymous = true };

    /// <summary>Anonymous callers allowed (groups, permissions and policy are then ignored).</summary>
    public bool AllowAnonymous { get; init; }

    /// <summary>The caller needs at least one of these groups.</summary>
    public IReadOnlyList<string> Groups { get; init; } = [];

    /// <summary>The caller needs all of these permissions.</summary>
    public IReadOnlyList<string> Permissions { get; init; } = [];

    /// <summary>Named authorization policy the caller must satisfy.</summary>
    public string? Policy { get; init; }
}

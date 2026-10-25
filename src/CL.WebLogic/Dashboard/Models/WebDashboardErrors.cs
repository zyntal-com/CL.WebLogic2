using CodeLogic.Core.Results;

namespace CL.WebLogic.Dashboard.Models;

/// <summary>
/// The error codes the dashboard endpoints understand. App stores return <see cref="Conflict"/>
/// on a version mismatch (answered with 409) and <see cref="Full"/> when they cannot take more (503).
/// </summary>
public static class WebDashboardErrors
{
    /// <summary>Nobody to own the layout (not signed in): 401.</summary>
    public const string NoOwnerCode = "weblogic.dashboard.no_owner";

    /// <summary>The dashboard key is malformed: 400.</summary>
    public const string InvalidKeyCode = "weblogic.dashboard.invalid_key";

    /// <summary>The layout is invalid (unknown widget, bad zone, too many items): 400.</summary>
    public const string InvalidCode = "weblogic.dashboard.invalid";

    /// <summary>The stored version is not the expected one: 409.</summary>
    public const string ConflictCode = "weblogic.dashboard.conflict";

    /// <summary>The store is out of room: 503.</summary>
    public const string FullCode = "weblogic.dashboard.full";

    /// <summary>The version-mismatch error for <see cref="Services.IWebDashboardLayoutStore.SaveAsync"/>.</summary>
    public static Error Conflict() => Error.Conflict(ConflictCode, "The dashboard was changed elsewhere; reload it.");

    /// <summary>The out-of-room error for <see cref="Services.IWebDashboardLayoutStore.SaveAsync"/>.</summary>
    public static Error Full(string message) => Error.Unavailable(FullCode, message);
}

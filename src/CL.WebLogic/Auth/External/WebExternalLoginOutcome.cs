using CL.WebLogic.Auth.Users;

namespace CL.WebLogic.Auth.External;

/// <summary>A verified external sign-in. The app decides what to do when <see cref="User"/> is null (register, link, refuse).</summary>
public sealed class WebExternalLoginOutcome
{
    /// <summary>Who signed in at the provider.</summary>
    public required WebExternalLoginInfo Info { get; init; }

    /// <summary>The linked user, when there is one.</summary>
    public WebUser? User { get; init; }

    /// <summary>The safe local URL to return to.</summary>
    public required string ReturnUrl { get; init; }
}

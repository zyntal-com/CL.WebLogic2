namespace CL.WebLogic.Security.Sessions;

/// <summary>
/// Optional capability of an <see cref="IWebSessionStore"/>: list a user's sessions. Enables
/// device lists (<c>request.Auth.ListSessionsAsync</c>), <c>Session.MaxPerUser</c> and new-device
/// notifications; without it those features are skipped (with a warning).
/// </summary>
public interface IWebSessionListStore
{
    /// <summary>Every stored session of <paramref name="userId"/> (expired ones may be included; callers filter).</summary>
    Task<IReadOnlyList<WebSessionRecord>> ListForUserAsync(string userId, CancellationToken cancellationToken);
}

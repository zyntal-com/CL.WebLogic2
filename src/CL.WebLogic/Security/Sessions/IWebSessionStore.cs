namespace CL.WebLogic.Security.Sessions;

/// <summary>
/// Session persistence, supplied by the app (database, Redis, files…). The in-memory default is
/// for development only.
/// </summary>
public interface IWebSessionStore
{
    /// <summary>Loads a session by token hash, or null.</summary>
    Task<WebSessionRecord?> GetAsync(string idHash, CancellationToken cancellationToken);

    /// <summary>Inserts or replaces a session.</summary>
    Task SaveAsync(WebSessionRecord record, CancellationToken cancellationToken);

    /// <summary>Deletes a session.</summary>
    Task DeleteAsync(string idHash, CancellationToken cancellationToken);

    /// <summary>Deletes every session of a user (sign out everywhere); returns the count.</summary>
    Task<int> DeleteForUserAsync(string userId, CancellationToken cancellationToken);

    /// <summary>Deletes sessions whose <see cref="WebSessionRecord.ExpiresAt"/> is before <paramref name="now"/>; returns the count.</summary>
    Task<int> DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken);
}

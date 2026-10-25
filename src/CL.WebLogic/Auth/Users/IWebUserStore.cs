namespace CL.WebLogic.Auth.Users;

/// <summary>
/// User persistence, supplied by the app. Optional capabilities: <see cref="IWebUserLockoutStore"/>
/// (atomic failure counting), <see cref="IWebUserEmailStore"/> (look-up by e-mail) and
/// <see cref="IWebUserExternalLoginStore"/> (external sign-in links).
/// </summary>
public interface IWebUserStore
{
    /// <summary>Finds a user by id.</summary>
    Task<WebUser?> FindByIdAsync(string userId, CancellationToken cancellationToken);

    /// <summary>Finds a user by login name (and, if the app wants, e-mail). Case-insensitive.</summary>
    Task<WebUser?> FindByLoginAsync(string login, CancellationToken cancellationToken);

    /// <summary>Creates a user; fails with <c>weblogic.auth.duplicate_user</c> when the name or e-mail is taken.</summary>
    Task<CodeLogic.Core.Results.Result> CreateAsync(WebUser user, CancellationToken cancellationToken);

    /// <summary>Saves a changed user.</summary>
    Task UpdateAsync(WebUser user, CancellationToken cancellationToken);
}

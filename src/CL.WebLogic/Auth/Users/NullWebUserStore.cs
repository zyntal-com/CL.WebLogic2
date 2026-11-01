using CodeLogic.Core.Results;
using CL.WebLogic.Auth.Accounts;

namespace CL.WebLogic.Auth.Users;

/// <summary>Default when the app registered no user store: nobody can sign in with a password and accounts cannot be created.</summary>
public sealed class NullWebUserStore : IWebUserStore
{
    /// <inheritdoc/>
    public Task<WebUser?> FindByIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<WebUser?>(null);

    /// <inheritdoc/>
    public Task<WebUser?> FindByLoginAsync(string login, CancellationToken cancellationToken) => Task.FromResult<WebUser?>(null);

    /// <inheritdoc/>
    public Task<Result> CreateAsync(WebUser user, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(Error.Unavailable(WebAuthErrorCodes.NoUserStore, "No user store is registered. Call web.UseUserStore<T>().")));

    /// <inheritdoc/>
    public Task UpdateAsync(WebUser user, CancellationToken cancellationToken) => Task.CompletedTask;
}

using CL.WebLogic.Auth.Users;
using CodeLogic.Core.Results;

namespace StarterWebsite.Infrastructure;

/// <summary>
/// Users in <c>users.json</c>, external logins in <c>external-logins.json</c>. Implements every
/// optional capability (lockout counters, e-mail lookup, external logins) the toolkit looks for.
/// </summary>
public sealed class JsonUserStore : IWebUserStore, IWebUserLockoutStore, IWebUserEmailStore, IWebUserExternalLoginStore, IDisposable
{
    private readonly JsonFileStore<WebUser> _users;
    private readonly JsonFileStore<string> _externalLogins;

    /// <summary>Stores in <paramref name="folder"/>.</summary>
    public JsonUserStore(StarterDataFolder folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        _users = new JsonFileStore<WebUser>(folder.File("users.json"));
        _externalLogins = new JsonFileStore<string>(folder.File("external-logins.json"));
    }

    /// <inheritdoc/>
    public Task<WebUser?> FindByIdAsync(string userId, CancellationToken cancellationToken) => _users.GetAsync(userId, cancellationToken);

    /// <inheritdoc/>
    public async Task<WebUser?> FindByLoginAsync(string login, CancellationToken cancellationToken) =>
        (await _users.WhereAsync(u => Same(u.UserName, login) || Same(u.Email, login), cancellationToken).ConfigureAwait(false)).FirstOrDefault();

    /// <inheritdoc/>
    public async Task<WebUser?> FindByEmailAsync(string email, CancellationToken cancellationToken) =>
        (await _users.WhereAsync(u => Same(u.Email, email), cancellationToken).ConfigureAwait(false)).FirstOrDefault();

    /// <inheritdoc/>
    public Task<Result> CreateAsync(WebUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        return _users.UpdateAsync(items =>
        {
            var taken = items.ContainsKey(user.Id) || items.Values.Any(u => Same(u.UserName, user.UserName) || (user.Email is not null && Same(u.Email, user.Email)));
            if (taken)
                return (false, Result.Failure(Error.Conflict("starter.duplicate_user", "The user name or e-mail address is already in use.")));
            items[user.Id] = user;
            return (true, Result.Success());
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task UpdateAsync(WebUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        return _users.SetAsync(user.Id, user, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<WebUser?> RecordFailedAccessAsync(string userId, int maxFailures, DateTimeOffset lockUntil, CancellationToken cancellationToken) =>
        _users.UpdateAsync(items =>
        {
            if (!items.TryGetValue(userId, out var user))
                return (false, (WebUser?)null);
            var count = user.FailedAccessCount + 1;
            var locks = maxFailures > 0 && count >= maxFailures;
            user = user with { FailedAccessCount = locks ? 0 : count, LockoutEnd = locks ? lockUntil : user.LockoutEnd };
            items[userId] = user;
            return (true, (WebUser?)user);
        }, cancellationToken);

    /// <inheritdoc/>
    public Task ResetFailedAccessAsync(string userId, CancellationToken cancellationToken) =>
        _users.UpdateAsync(items =>
        {
            if (!items.TryGetValue(userId, out var user) || (user.FailedAccessCount == 0 && user.LockoutEnd is null))
                return (false, false);
            items[userId] = user with { FailedAccessCount = 0, LockoutEnd = null };
            return (true, true);
        }, cancellationToken);

    /// <inheritdoc/>
    public async Task<WebUser?> FindByExternalLoginAsync(string provider, string providerKey, CancellationToken cancellationToken)
    {
        var userId = await _externalLogins.GetAsync(provider + "|" + providerKey, cancellationToken).ConfigureAwait(false);
        return userId is null ? null : await _users.GetAsync(userId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task AddExternalLoginAsync(string userId, string provider, string providerKey, CancellationToken cancellationToken) =>
        _externalLogins.SetAsync(provider + "|" + providerKey, userId, cancellationToken);

    /// <summary>Number of accounts (for the home page).</summary>
    public async Task<int> CountAsync(CancellationToken cancellationToken) =>
        (await _users.WhereAsync(_ => true, cancellationToken).ConfigureAwait(false)).Count;

    /// <inheritdoc/>
    public void Dispose()
    {
        _users.Dispose();
        _externalLogins.Dispose();
    }

    private static bool Same(string? a, string? b) => a is not null && b is not null && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}

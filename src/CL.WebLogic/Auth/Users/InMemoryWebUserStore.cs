using System.Collections.Concurrent;
using CL.WebLogic.Security.Services;
using CodeLogic.Core.Results;

namespace CL.WebLogic.Auth.Users;

/// <summary>Development user store (process memory). Supports every optional capability.</summary>
public sealed class InMemoryWebUserStore : IWebUserStore, IWebUserLockoutStore, IWebUserEmailStore, IWebUserExternalLoginStore, IWebUserDeletionStore, IWebDevelopmentStore
{
    private readonly ConcurrentDictionary<string, WebUser> _users = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _externalLogins = new(StringComparer.Ordinal);
    private readonly object _lock = new();

    /// <inheritdoc/>
    public Task<WebUser?> FindByIdAsync(string userId, CancellationToken cancellationToken) =>
        Task.FromResult(_users.TryGetValue(userId, out var user) ? user : null);

    /// <inheritdoc/>
    public Task<WebUser?> FindByLoginAsync(string login, CancellationToken cancellationToken) =>
        Task.FromResult(_users.Values.FirstOrDefault(u =>
            string.Equals(u.UserName, login, StringComparison.OrdinalIgnoreCase)
            || string.Equals(u.Email, login, StringComparison.OrdinalIgnoreCase)));

    /// <inheritdoc/>
    public Task<WebUser?> FindByEmailAsync(string email, CancellationToken cancellationToken) =>
        Task.FromResult(_users.Values.FirstOrDefault(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)));

    /// <inheritdoc/>
    public Task<Result> CreateAsync(WebUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        lock (_lock)
        {
            var taken = _users.Values.Any(u => string.Equals(u.UserName, user.UserName, StringComparison.OrdinalIgnoreCase)
                || (user.Email is not null && string.Equals(u.Email, user.Email, StringComparison.OrdinalIgnoreCase)));
            if (taken || !_users.TryAdd(user.Id, user))
                return Task.FromResult(Result.Failure(Error.Conflict("weblogic.auth.duplicate_user", "The user name or e-mail address is already in use.")));
        }
        return Task.FromResult(Result.Success());
    }

    /// <inheritdoc/>
    public Task UpdateAsync(WebUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        _users[user.Id] = user;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<bool> DeleteAsync(string userId, CancellationToken cancellationToken)
    {
        foreach (var (key, owner) in _externalLogins)
        {
            if (owner == userId)
                _externalLogins.TryRemove(key, out _);
        }
        return Task.FromResult(_users.TryRemove(userId, out _));
    }

    /// <inheritdoc/>
    public Task<WebUser?> RecordFailedAccessAsync(string userId, int maxFailures, DateTimeOffset lockUntil, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (!_users.TryGetValue(userId, out var user))
                return Task.FromResult<WebUser?>(null);
            var count = user.FailedAccessCount + 1;
            user = user with
            {
                FailedAccessCount = maxFailures > 0 && count >= maxFailures ? 0 : count,
                LockoutEnd = maxFailures > 0 && count >= maxFailures ? lockUntil : user.LockoutEnd
            };
            _users[userId] = user;
            return Task.FromResult<WebUser?>(user);
        }
    }

    /// <inheritdoc/>
    public Task ResetFailedAccessAsync(string userId, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (_users.TryGetValue(userId, out var user))
                _users[userId] = user with { FailedAccessCount = 0, LockoutEnd = null };
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<WebUser?> FindByExternalLoginAsync(string provider, string providerKey, CancellationToken cancellationToken) =>
        Task.FromResult(_externalLogins.TryGetValue(provider + "|" + providerKey, out var userId) && _users.TryGetValue(userId, out var user) ? user : null);

    /// <inheritdoc/>
    public Task AddExternalLoginAsync(string userId, string provider, string providerKey, CancellationToken cancellationToken)
    {
        _externalLogins[provider + "|" + providerKey] = userId;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<bool> RemoveExternalLoginAsync(string userId, string provider, CancellationToken cancellationToken)
    {
        var removed = false;
        foreach (var (key, owner) in _externalLogins)
        {
            if (owner == userId && key.StartsWith(provider + "|", StringComparison.Ordinal))
                removed |= _externalLogins.TryRemove(key, out _);
        }
        return Task.FromResult(removed);
    }
}

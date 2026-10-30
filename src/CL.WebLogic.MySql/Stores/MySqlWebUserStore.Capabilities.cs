using CL.WebLogic.Auth.Users;
using CL.WebLogic.MySql.Entities;
using CL.WebLogic.MySql.Services;

namespace CL.WebLogic.MySql.Stores;

public sealed partial class MySqlWebUserStore
{
    /// <inheritdoc/>
    public async Task<WebUser?> RecordFailedAccessAsync(string userId, int maxFailures, DateTimeOffset lockUntil, CancellationToken cancellationToken)
    {
        // One atomic UPDATE, so parallel failures on several nodes cannot skip the limit. The lockout
        // is assigned before the count, so both read the old count; reaching the maximum locks the
        // account and resets the count (same rules as the in-memory store).
        var users = _db.Query<UserRow>().Where(u => u.Id == userId);
        if (maxFailures > 0)
        {
            DateTime? until = lockUntil.UtcDateTime;
            WebMySqlDatabase.Value(await users.UpdateAsync(u => new UserRow
            {
                LockoutEndUtc = u.FailedAccessCount + 1 >= maxFailures ? until : u.LockoutEndUtc,
                FailedAccessCount = u.FailedAccessCount + 1 >= maxFailures ? 0 : u.FailedAccessCount + 1
            }, cancellationToken).ConfigureAwait(false));
        }
        else
        {
            WebMySqlDatabase.Value(await users.UpdateAsync(u => new UserRow { FailedAccessCount = u.FailedAccessCount + 1 }, cancellationToken).ConfigureAwait(false));
        }
        return await FindByIdAsync(userId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task ResetFailedAccessAsync(string userId, CancellationToken cancellationToken) =>
        WebMySqlDatabase.Value(await _db.Query<UserRow>().Where(u => u.Id == userId)
            .UpdateAsync(u => new UserRow { FailedAccessCount = 0, LockoutEndUtc = null }, cancellationToken).ConfigureAwait(false));

    /// <inheritdoc/>
    public async Task<WebUser?> FindByExternalLoginAsync(string provider, string providerKey, CancellationToken cancellationToken)
    {
        var link = WebMySqlDatabase.Value(await _db.Repository<UserLoginRow>().GetByIdAsync(LoginKey(provider, providerKey), cancellationToken).ConfigureAwait(false));
        return link is null ? null : await FindByIdAsync(link.UserId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task AddExternalLoginAsync(string userId, string provider, string providerKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        WebMySqlDatabase.Value(await _db.Repository<UserLoginRow>().UpsertAsync(new UserLoginRow
        {
            LoginKey = LoginKey(provider, providerKey),
            UserId = userId,
            CreatedUtc = DateTime.UtcNow
        }, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc/>
    public async Task<bool> RemoveExternalLoginAsync(string userId, string provider, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        var prefix = provider.Trim().ToLowerInvariant() + ":";
        var rows = WebMySqlDatabase.Value(await _db.Query<UserLoginRow>().Where(l => l.UserId == userId).ToListAsync(cancellationToken).ConfigureAwait(false));
        var removed = false;
        foreach (var key in rows.Select(r => r.LoginKey).Where(k => k.StartsWith(prefix, StringComparison.Ordinal)))
        {
            WebMySqlDatabase.Value(await _db.Query<UserLoginRow>().Where(l => l.LoginKey == key && l.UserId == userId).DeleteAsync(cancellationToken).ConfigureAwait(false));
            removed = true;
        }
        return removed;
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteAsync(string userId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        await using var transaction = await _db.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        WebMySqlDatabase.Value(await _db.Query<UserLoginRow>(transaction).Where(l => l.UserId == userId).DeleteAsync(cancellationToken).ConfigureAwait(false));
        var deleted = WebMySqlDatabase.Value(await _db.Query<UserRow>(transaction).Where(u => u.Id == userId).DeleteAsync(cancellationToken).ConfigureAwait(false));
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return deleted > 0;
    }

    private static string LoginKey(string provider, string providerKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrEmpty(providerKey);
        var key = provider.Trim().ToLowerInvariant() + ":" + providerKey;
        return key.Length <= 400 ? key : throw new ArgumentException("The external login key is too long.", nameof(providerKey));
    }
}

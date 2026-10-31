using CL.WebLogic.Auth.Users;
using CL.WebLogic.Data.Entities;
using CL.WebLogic.Data.Services;

namespace CL.WebLogic.Data.Stores;

public sealed partial class DataWebUserStore
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
            WebDataDatabase.Value(await users.UpdateAsync(u => new UserRow
            {
                LockoutEndUtc = u.FailedAccessCount + 1 >= maxFailures ? until : u.LockoutEndUtc,
                FailedAccessCount = u.FailedAccessCount + 1 >= maxFailures ? 0 : u.FailedAccessCount + 1
            }, cancellationToken).ConfigureAwait(false));
        }
        else
        {
            WebDataDatabase.Value(await users.UpdateAsync(u => new UserRow { FailedAccessCount = u.FailedAccessCount + 1 }, cancellationToken).ConfigureAwait(false));
        }
        return await FindByIdAsync(userId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task ResetFailedAccessAsync(string userId, CancellationToken cancellationToken) =>
        WebDataDatabase.Value(await _db.Query<UserRow>().Where(u => u.Id == userId)
            .UpdateAsync(u => new UserRow { FailedAccessCount = 0, LockoutEndUtc = null }, cancellationToken).ConfigureAwait(false));

    /// <inheritdoc/>
    public async Task<WebUser?> FindByExternalLoginAsync(string provider, string providerKey, CancellationToken cancellationToken)
    {
        var key = LoginKey(provider, providerKey);
        var link = await _db.FindAsync<UserLoginRow>(l => l.LoginKey == key, cancellationToken).ConfigureAwait(false);
        return link is null ? null : await FindByIdAsync(link.UserId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task AddExternalLoginAsync(string userId, string provider, string providerKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        WebDataDatabase.Value(await _db.Repository<UserLoginRow>().UpsertAsync(new UserLoginRow
        {
            LoginKey = LoginKey(provider, providerKey),
            UserId = userId,
            CreatedUtc = DateTime.UtcNow
        }, ct: cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc/>
    public async Task<bool> RemoveExternalLoginAsync(string userId, string provider, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        var prefix = provider.Trim().ToLowerInvariant() + ":";
        var rows = WebDataDatabase.Value(await _db.Query<UserLoginRow>().Where(l => l.UserId == userId).ToListAsync(cancellationToken).ConfigureAwait(false));
        var removed = false;
        foreach (var key in rows.Select(r => r.LoginKey).Where(k => k.StartsWith(prefix, StringComparison.Ordinal)))
        {
            WebDataDatabase.Value(await _db.Query<UserLoginRow>().Where(l => l.LoginKey == key && l.UserId == userId).DeleteAsync(ct: cancellationToken).ConfigureAwait(false));
            removed = true;
        }
        return removed;
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteAsync(string userId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        return await _db.InTransactionAsync(async (transaction, ct) =>
        {
            WebDataDatabase.Value(await transaction.Query<UserLoginRow>().Where(l => l.UserId == userId).DeleteAsync(ct: ct).ConfigureAwait(false));
            return WebDataDatabase.Value(await transaction.Query<UserRow>().Where(u => u.Id == userId).DeleteAsync(ct: ct).ConfigureAwait(false)) > 0;
        }, cancellationToken).ConfigureAwait(false);
    }

    private static string LoginKey(string provider, string providerKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrEmpty(providerKey);
        var key = provider.Trim().ToLowerInvariant() + ":" + providerKey;
        return key.Length <= 400 ? key : throw new ArgumentException("The external login key is too long.", nameof(providerKey));
    }
}

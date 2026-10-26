using CL.WebLogic.Auth.Users;
using CL.WebLogic.MySql.Entities;
using CL.WebLogic.MySql.Services;

namespace CL.WebLogic.MySql.Stores;

public sealed partial class MySqlWebUserStore
{
    // Assignments run left to right and see earlier ones: count first, then lock when it reaches the
    // maximum, then reset the count of a locked account (same rules as the in-memory store).
    private const string RecordFailureSql = """
        UPDATE wl_users SET
          failed_access_count = failed_access_count + 1,
          lockout_end_utc = IF(@max > 0 AND failed_access_count >= @max, @until, lockout_end_utc),
          failed_access_count = IF(@max > 0 AND failed_access_count >= @max, 0, failed_access_count)
        WHERE id = @id
        """;

    /// <inheritdoc/>
    public async Task<WebUser?> RecordFailedAccessAsync(string userId, int maxFailures, DateTimeOffset lockUntil, CancellationToken cancellationToken)
    {
        await _db.ExecuteAsync(RecordFailureSql, new Dictionary<string, object?>
        {
            ["@id"] = userId,
            ["@max"] = maxFailures,
            ["@until"] = lockUntil.UtcDateTime
        }, cancellationToken).ConfigureAwait(false);
        return await FindByIdAsync(userId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task ResetFailedAccessAsync(string userId, CancellationToken cancellationToken) =>
        _db.ExecuteAsync("UPDATE wl_users SET failed_access_count = 0, lockout_end_utc = NULL WHERE id = @id",
            new Dictionary<string, object?> { ["@id"] = userId }, cancellationToken);

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

    private static string LoginKey(string provider, string providerKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrEmpty(providerKey);
        var key = provider.Trim().ToLowerInvariant() + ":" + providerKey;
        return key.Length <= 400 ? key : throw new ArgumentException("The external login key is too long.", nameof(providerKey));
    }
}

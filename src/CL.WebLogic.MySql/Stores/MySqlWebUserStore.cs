using CL.MySQL2.Models;
using CL.WebLogic.Auth.Users;
using CL.WebLogic.MySql.Entities;
using CL.WebLogic.MySql.Services;
using CodeLogic.Core.Results;

namespace CL.WebLogic.MySql.Stores;

/// <summary>
/// Users in <c>wl_users</c> (lookup columns plus the record as JSON) and external logins in
/// <c>wl_user_logins</c>. User names and e-mail addresses are unique case-insensitively; failed
/// sign-ins are counted with one atomic statement so concurrent attempts cannot undercount.
/// </summary>
public sealed partial class MySqlWebUserStore : IWebUserStore, IWebUserLockoutStore, IWebUserEmailStore, IWebUserExternalLoginStore, IWebUserDeletionStore
{
    private readonly WebMySqlDatabase _db;

    /// <summary>Creates the store.</summary>
    public MySqlWebUserStore(WebMySqlDatabase db) => _db = db ?? throw new ArgumentNullException(nameof(db));

    /// <inheritdoc/>
    public async Task<WebUser?> FindByIdAsync(string userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(userId))
            return null;
        var row = WebMySqlDatabase.Value(await _db.Repository<UserRow>().GetByIdAsync(userId, cancellationToken).ConfigureAwait(false));
        return row is null ? null : ToUser(row);
    }

    /// <inheritdoc/>
    public async Task<WebUser?> FindByLoginAsync(string login, CancellationToken cancellationToken)
    {
        var normalized = Normalize(login);
        if (normalized.Length == 0)
            return null;
        var row = WebMySqlDatabase.Value(await _db.Query<UserRow>()
            .Where(u => u.NormalizedUserName == normalized || u.NormalizedEmail == normalized)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false));
        return row is null ? null : ToUser(row);
    }

    /// <inheritdoc/>
    public async Task<Result> CreateAsync(WebUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        var outcome = WebMySqlDatabase.Value(await _db.Repository<UserRow>().InsertIgnoreAsync(ToRow(user), cancellationToken).ConfigureAwait(false));
        return outcome == InsertOutcome.Inserted
            ? Result.Success()
            : Result.Failure(Error.Conflict("weblogic.auth.duplicate_user", "The user name or e-mail address is already in use."));
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(WebUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        WebMySqlDatabase.Value(await _db.Repository<UserRow>().UpdateAsync(ToRow(user), cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc/>
    public async Task<WebUser?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var normalized = Normalize(email);
        if (normalized.Length == 0)
            return null;
        var row = WebMySqlDatabase.Value(await _db.Query<UserRow>().Where(u => u.NormalizedEmail == normalized)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false));
        return row is null ? null : ToUser(row);
    }

    internal static string Normalize(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();

    private static UserRow ToRow(WebUser user) => new()
    {
        Id = user.Id,
        NormalizedUserName = Normalize(user.UserName),
        NormalizedEmail = string.IsNullOrWhiteSpace(user.Email) ? null : Normalize(user.Email),
        TenantId = user.TenantId,
        FailedAccessCount = user.FailedAccessCount,
        LockoutEndUtc = user.LockoutEnd?.UtcDateTime,
        CreatedUtc = user.CreatedAt.UtcDateTime,
        RecordJson = WebMySqlJson.Write(user)
    };

    private static WebUser ToUser(UserRow row) => WebMySqlJson.Read<WebUser>(row.RecordJson) with
    {
        FailedAccessCount = row.FailedAccessCount,
        LockoutEnd = row.LockoutEndUtc is { } end ? WebMySqlDatabase.FromUtc(end) : null
    };
}

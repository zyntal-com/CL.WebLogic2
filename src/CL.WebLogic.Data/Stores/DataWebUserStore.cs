using CL.Data.Query;
using CL.WebLogic.Auth.Accounts;
using CL.WebLogic.Auth.Users;
using CL.WebLogic.Data.Entities;
using CL.WebLogic.Data.Services;
using CodeLogic.Core.Results;

namespace CL.WebLogic.Data.Stores;

/// <summary>
/// Users in <c>wl_users</c> (lookup columns plus the record as JSON) and external logins in
/// <c>wl_user_logins</c>. User names and e-mail addresses are unique case-insensitively; failed
/// sign-ins are counted with one atomic statement so concurrent attempts cannot undercount.
/// </summary>
public sealed partial class DataWebUserStore : IWebUserStore, IWebUserLockoutStore, IWebUserEmailStore, IWebUserExternalLoginStore, IWebUserDeletionStore
{
    private readonly WebDataDatabase _db;

    /// <summary>Creates the store.</summary>
    public DataWebUserStore(WebDataDatabase db) => _db = db ?? throw new ArgumentNullException(nameof(db));

    /// <inheritdoc/>
    public async Task<WebUser?> FindByIdAsync(string userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(userId))
            return null;
        var row = WebDataDatabase.Value(await _db.Query<UserRow>().Where(r => r.Id == userId).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false));
        return row is null ? null : ToUser(row);
    }

    /// <inheritdoc/>
    public async Task<WebUser?> FindByLoginAsync(string login, CancellationToken cancellationToken)
    {
        var normalized = Normalize(login);
        if (normalized.Length == 0)
            return null;
        var row = WebDataDatabase.Value(await _db.Query<UserRow>()
            .Where(u => u.NormalizedUserName == normalized || u.NormalizedEmail == normalized)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false));
        return row is null ? null : ToUser(row);
    }

    /// <inheritdoc/>
    public async Task<Result> CreateAsync(WebUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        // A clash on the id is ignored by insert-ignore on every database; a clash on the unique user
        // name or e-mail is ignored on MySQL but reported as data.duplicate_key elsewhere. Both are a conflict.
        var outcome = await _db.Repository<UserRow>().InsertIgnoreAsync(ToRow(user), cancellationToken).ConfigureAwait(false);
        if (outcome.IsSuccess && outcome.Value == InsertOutcome.Inserted)
            return Result.Success();
        if (outcome.IsSuccess || WebDataDatabase.IsDuplicate(outcome.Error))
            return Result.Failure(Error.Conflict(WebAuthErrorCodes.DuplicateUser, "The user name or e-mail address is already in use."));
        WebDataDatabase.Value(outcome);
        return Result.Failure(outcome.Error!);
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(WebUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        WebDataDatabase.Value(await _db.Repository<UserRow>().UpdateAsync(ToRow(user), cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc/>
    public async Task<WebUser?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var normalized = Normalize(email);
        if (normalized.Length == 0)
            return null;
        var row = WebDataDatabase.Value(await _db.Query<UserRow>().Where(u => u.NormalizedEmail == normalized)
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
        RecordJson = WebDataJson.Write(user)
    };

    private static WebUser ToUser(UserRow row) => WebDataJson.Read<WebUser>(row.RecordJson) with
    {
        FailedAccessCount = row.FailedAccessCount,
        LockoutEnd = row.LockoutEndUtc is { } end ? WebDataDatabase.FromUtc(end) : null
    };
}

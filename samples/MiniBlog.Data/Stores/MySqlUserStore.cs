using System.Text.Json;
using CL.WebLogic.Auth.Users;
using CodeLogic.Core.Results;
using MiniBlog.Data.Entities;

namespace MiniBlog.Data.Stores;

/// <summary>WebLogic users in <c>miniblog_users</c> (with lockout counters and e-mail lookup).</summary>
public sealed class MySqlUserStore : IWebUserStore, IWebUserLockoutStore, IWebUserEmailStore
{
    private readonly BlogDatabase _db;

    /// <summary>Creates the store.</summary>
    public MySqlUserStore(BlogDatabase db) => _db = db;

    /// <inheritdoc/>
    public async Task<WebUser?> FindByIdAsync(string userId, CancellationToken cancellationToken)
    {
        var row = await _db.MySql.GetRepository<UserEntity>(_db.ConnectionId).GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        return Unwrap(row) is { } entity ? ToUser(entity) : null;
    }

    /// <inheritdoc/>
    public async Task<WebUser?> FindByLoginAsync(string login, CancellationToken cancellationToken)
    {
        var rows = await _db.MySql.Query<UserEntity>(_db.ConnectionId).Where(u => u.UserName == login || u.Email == login).Take(1).ToListAsync(cancellationToken).ConfigureAwait(false);
        return Unwrap(rows)?.FirstOrDefault() is { } entity ? ToUser(entity) : null;
    }

    /// <inheritdoc/>
    public async Task<WebUser?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var rows = await _db.MySql.Query<UserEntity>(_db.ConnectionId).Where(u => u.Email == email).Take(1).ToListAsync(cancellationToken).ConfigureAwait(false);
        return Unwrap(rows)?.FirstOrDefault() is { } entity ? ToUser(entity) : null;
    }

    /// <inheritdoc/>
    public async Task<Result> CreateAsync(WebUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        var inserted = await _db.MySql.GetRepository<UserEntity>(_db.ConnectionId).InsertIgnoreAsync(ToEntity(user), cancellationToken).ConfigureAwait(false);
        if (inserted.IsFailure)
            return Result.Failure(inserted.Error!);
        return inserted.Value == CL.MySQL2.Models.InsertOutcome.Inserted
            ? Result.Success()
            : Result.Failure(Error.Conflict("miniblog.duplicate_user", "The user name or e-mail address is already in use."));
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(WebUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        Unwrap(await _db.MySql.GetRepository<UserEntity>(_db.ConnectionId).UpdateAsync(ToEntity(user), cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc/>
    public async Task<WebUser?> RecordFailedAccessAsync(string userId, int maxFailures, DateTimeOffset lockUntil, CancellationToken cancellationToken)
    {
        var user = await FindByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null)
            return null;
        var count = user.FailedAccessCount + 1;
        var locks = maxFailures > 0 && count >= maxFailures;
        user = user with { FailedAccessCount = locks ? 0 : count, LockoutEnd = locks ? lockUntil : user.LockoutEnd };
        await UpdateAsync(user, cancellationToken).ConfigureAwait(false);
        return user;
    }

    /// <inheritdoc/>
    public async Task ResetFailedAccessAsync(string userId, CancellationToken cancellationToken)
    {
        var updated = await _db.MySql.Query<UserEntity>(_db.ConnectionId)
            .Where(u => u.Id == userId)
            .UpdateAsync(new Dictionary<string, object?> { ["failed_access_count"] = 0, ["lockout_end_utc"] = null }, cancellationToken)
            .ConfigureAwait(false);
        Unwrap(updated);
    }

    private static T? Unwrap<T>(Result<T> result) =>
        result.IsSuccess ? result.Value : throw new InvalidOperationException("MiniBlog user store: " + result.Error!.Message);

    private static WebUser ToUser(UserEntity e) => new()
    {
        Id = e.Id,
        UserName = e.UserName,
        Email = e.Email,
        EmailConfirmed = e.EmailConfirmed,
        PasswordHash = e.PasswordHash,
        SecurityStamp = e.SecurityStamp,
        Groups = JsonSerializer.Deserialize<List<string>>(e.GroupsJson) ?? [],
        Permissions = JsonSerializer.Deserialize<List<string>>(e.PermissionsJson) ?? [],
        IsDisabled = e.IsDisabled,
        RequiresPasswordChange = e.RequiresPasswordChange,
        LockoutEnd = e.LockoutEndUtc is { } end ? new DateTimeOffset(DateTime.SpecifyKind(end, DateTimeKind.Utc)) : null,
        FailedAccessCount = e.FailedAccessCount,
        TwoFactorSecret = e.TwoFactorSecret,
        CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(e.CreatedUtc, DateTimeKind.Utc))
    };

    private static UserEntity ToEntity(WebUser u) => new()
    {
        Id = u.Id,
        UserName = u.UserName,
        Email = u.Email,
        EmailConfirmed = u.EmailConfirmed,
        PasswordHash = u.PasswordHash,
        SecurityStamp = u.SecurityStamp,
        GroupsJson = JsonSerializer.Serialize(u.Groups),
        PermissionsJson = JsonSerializer.Serialize(u.Permissions),
        IsDisabled = u.IsDisabled,
        RequiresPasswordChange = u.RequiresPasswordChange,
        LockoutEndUtc = u.LockoutEnd?.UtcDateTime,
        FailedAccessCount = u.FailedAccessCount,
        TwoFactorSecret = u.TwoFactorSecret,
        CreatedUtc = u.CreatedAt.UtcDateTime
    };
}

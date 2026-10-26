using System.Security.Cryptography;
using System.Text;
using CL.MySQL2.Models;
using CL.WebLogic.Auth.Passkeys;
using CL.WebLogic.MySql.Entities;
using CL.WebLogic.MySql.Services;

namespace CL.WebLogic.MySql.Stores;

/// <summary>
/// Passkeys in <c>wl_passkeys</c>, keyed by the SHA-256 of the credential id. Registration is an
/// <c>INSERT IGNORE</c> (one owner per credential) and each sign-in a conditional <c>UPDATE</c> on the
/// counter, so two nodes can never both accept the same counter value.
/// </summary>
public sealed class MySqlWebPasskeyStore : IWebPasskeyStore
{
    private readonly WebMySqlDatabase _db;

    /// <summary>Creates the store.</summary>
    public MySqlWebPasskeyStore(WebMySqlDatabase db) => _db = db ?? throw new ArgumentNullException(nameof(db));

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WebPasskey>> ListAsync(string userId, CancellationToken cancellationToken)
    {
        var rows = WebMySqlDatabase.Value(await _db.Query<PasskeyRow>().Where(p => p.UserId == userId).ToListAsync(cancellationToken).ConfigureAwait(false));
        return [.. rows.Select(FromRow).OrderBy(p => p.CreatedAt)];
    }

    /// <inheritdoc/>
    public async Task<WebPasskey?> FindAsync(string credentialId, CancellationToken cancellationToken)
    {
        var row = WebMySqlDatabase.Value(await _db.Repository<PasskeyRow>().GetByIdAsync(Hash(credentialId), cancellationToken).ConfigureAwait(false));
        return row is null || row.CredentialId != credentialId ? null : FromRow(row);
    }

    /// <inheritdoc/>
    public async Task<bool> AddAsync(WebPasskey passkey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(passkey);
        var row = new PasskeyRow
        {
            IdHash = Hash(passkey.CredentialId),
            CredentialId = passkey.CredentialId,
            UserId = passkey.UserId,
            SignCount = passkey.SignCount,
            BackedUp = passkey.BackedUp ? 1 : 0,
            Name = passkey.Name,
            CreatedUtc = passkey.CreatedAt.UtcDateTime,
            LastUsedUtc = passkey.LastUsedAt?.UtcDateTime,
            RecordJson = WebMySqlJson.Write(passkey)
        };
        return WebMySqlDatabase.Value(await _db.Repository<PasskeyRow>().InsertIgnoreAsync(row, cancellationToken).ConfigureAwait(false)) == InsertOutcome.Inserted;
    }

    /// <inheritdoc/>
    public async Task<bool> TryRecordUseAsync(string credentialId, long expectedSignCount, long signCount, bool backedUp, DateTimeOffset usedAt, CancellationToken cancellationToken) =>
        await _db.ExecuteAsync(
            "UPDATE wl_passkeys SET sign_count = @count, backed_up = @backed, last_used_utc = @used WHERE id_hash = @id AND sign_count = @expected",
            new Dictionary<string, object?>
            {
                ["@count"] = signCount,
                ["@backed"] = backedUp ? 1 : 0,
                ["@used"] = usedAt.UtcDateTime,
                ["@id"] = Hash(credentialId),
                ["@expected"] = expectedSignCount
            }, cancellationToken).ConfigureAwait(false) == 1;

    /// <inheritdoc/>
    public async Task<bool> RenameAsync(string userId, string credentialId, string name, CancellationToken cancellationToken) =>
        await _db.ExecuteAsync("UPDATE wl_passkeys SET name = @name WHERE id_hash = @id AND user_id = @user",
            new Dictionary<string, object?> { ["@name"] = name, ["@id"] = Hash(credentialId), ["@user"] = userId }, cancellationToken).ConfigureAwait(false) == 1;

    /// <inheritdoc/>
    public async Task<bool> DeleteAsync(string userId, string credentialId, CancellationToken cancellationToken) =>
        await _db.ExecuteAsync("DELETE FROM wl_passkeys WHERE id_hash = @id AND user_id = @user",
            new Dictionary<string, object?> { ["@id"] = Hash(credentialId), ["@user"] = userId }, cancellationToken).ConfigureAwait(false) == 1;

    private static WebPasskey FromRow(PasskeyRow row) => WebMySqlJson.Read<WebPasskey>(row.RecordJson) with
    {
        SignCount = row.SignCount,
        BackedUp = row.BackedUp != 0,
        Name = row.Name,
        LastUsedAt = row.LastUsedUtc is { } used ? WebMySqlDatabase.FromUtc(used) : null
    };

    private static string Hash(string credentialId) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(credentialId ?? "")));
}

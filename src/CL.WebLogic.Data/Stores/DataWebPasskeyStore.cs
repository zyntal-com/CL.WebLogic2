using System.Security.Cryptography;
using System.Text;
using CL.Data.Query;
using CL.WebLogic.Auth.Passkeys;
using CL.WebLogic.Data.Entities;
using CL.WebLogic.Data.Services;

namespace CL.WebLogic.Data.Stores;

/// <summary>
/// Passkeys in <c>wl_passkeys</c>, keyed by the SHA-256 of the credential id. Registration is an
/// <c>INSERT IGNORE</c> (one owner per credential) and each sign-in a conditional <c>UPDATE</c> on the
/// counter, so two nodes can never both accept the same counter value.
/// </summary>
public sealed class DataWebPasskeyStore : IWebPasskeyStore
{
    private readonly WebDataDatabase _db;

    /// <summary>Creates the store.</summary>
    public DataWebPasskeyStore(WebDataDatabase db) => _db = db ?? throw new ArgumentNullException(nameof(db));

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WebPasskey>> ListAsync(string userId, CancellationToken cancellationToken)
    {
        var rows = WebDataDatabase.Value(await _db.Query<PasskeyRow>().Where(p => p.UserId == userId).ToListAsync(cancellationToken).ConfigureAwait(false));
        return [.. rows.Select(FromRow).OrderBy(p => p.CreatedAt)];
    }

    /// <inheritdoc/>
    public async Task<WebPasskey?> FindAsync(string credentialId, CancellationToken cancellationToken)
    {
        var id = Hash(credentialId);
        var row = await _db.FindAsync<PasskeyRow>(p => p.IdHash == id, cancellationToken).ConfigureAwait(false);
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
            RecordJson = WebDataJson.Write(passkey)
        };
        return WebDataDatabase.Value(await _db.Repository<PasskeyRow>().InsertIgnoreAsync(row, cancellationToken).ConfigureAwait(false)) == InsertOutcome.Inserted;
    }

    /// <inheritdoc/>
    public async Task<bool> TryRecordUseAsync(string credentialId, long expectedSignCount, long signCount, bool backedUp, DateTimeOffset usedAt, CancellationToken cancellationToken)
    {
        // Compare-and-swap: only the caller that read the current counter wins. An authenticator that
        // does not count (the counter stays the same) may write identical values, which MySQL reports
        // as 0 changed rows; then the row still matching the expected counter is the answer.
        var id = Hash(credentialId);
        var backed = backedUp ? 1 : 0;
        DateTime? used = usedAt.UtcDateTime;
        var current = _db.Query<PasskeyRow>().Where(p => p.IdHash == id && p.SignCount == expectedSignCount);
        if (WebDataDatabase.Value(await current.UpdateAsync(p => new PasskeyRow { SignCount = signCount, BackedUp = backed, LastUsedUtc = used }, cancellationToken).ConfigureAwait(false)) == 1)
            return true;
        return signCount == expectedSignCount && WebDataDatabase.Value(await current.AnyAsync(cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc/>
    public async Task<bool> RenameAsync(string userId, string credentialId, string name, CancellationToken cancellationToken)
    {
        // MySQL counts only changed rows, so renaming to the same name reports 0: then check that it exists.
        var id = Hash(credentialId);
        var owned = _db.Query<PasskeyRow>().Where(p => p.IdHash == id && p.UserId == userId);
        return WebDataDatabase.Value(await owned.UpdateAsync(p => new PasskeyRow { Name = name }, cancellationToken).ConfigureAwait(false)) == 1
            || WebDataDatabase.Value(await owned.AnyAsync(cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteAsync(string userId, string credentialId, CancellationToken cancellationToken)
    {
        var id = Hash(credentialId);
        return WebDataDatabase.Value(await _db.Query<PasskeyRow>().Where(p => p.IdHash == id && p.UserId == userId).DeleteAsync(ct: cancellationToken).ConfigureAwait(false)) == 1;
    }

    private static WebPasskey FromRow(PasskeyRow row) => WebDataJson.Read<WebPasskey>(row.RecordJson) with
    {
        SignCount = row.SignCount,
        BackedUp = row.BackedUp != 0,
        Name = row.Name,
        LastUsedAt = row.LastUsedUtc is { } used ? WebDataDatabase.FromUtc(used) : null
    };

    private static string Hash(string credentialId) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(credentialId ?? "")));
}

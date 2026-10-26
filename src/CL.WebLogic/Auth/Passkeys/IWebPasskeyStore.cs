namespace CL.WebLogic.Auth.Passkeys;

/// <summary>
/// Stores passkeys. Supplied by the app (<c>web.UsePasskeyStore&lt;T&gt;()</c>); the MySQL add-on has
/// one. Credential ids are unique across all users.
/// </summary>
public interface IWebPasskeyStore
{
    /// <summary>The user's passkeys.</summary>
    Task<IReadOnlyList<WebPasskey>> ListAsync(string userId, CancellationToken cancellationToken);

    /// <summary>The passkey with this credential id, or null.</summary>
    Task<WebPasskey?> FindAsync(string credentialId, CancellationToken cancellationToken);

    /// <summary>Adds a passkey; false when the credential id is already registered (to anyone).</summary>
    Task<bool> AddAsync(WebPasskey passkey, CancellationToken cancellationToken);

    /// <summary>
    /// Records a sign-in atomically: only when the stored counter still equals
    /// <paramref name="expectedSignCount"/> (another node did not use it meanwhile). False otherwise.
    /// </summary>
    Task<bool> TryRecordUseAsync(string credentialId, long expectedSignCount, long signCount, bool backedUp, DateTimeOffset usedAt, CancellationToken cancellationToken);

    /// <summary>Renames one of the user's passkeys; false when it is not theirs.</summary>
    Task<bool> RenameAsync(string userId, string credentialId, string name, CancellationToken cancellationToken);

    /// <summary>Deletes one of the user's passkeys; false when it is not theirs.</summary>
    Task<bool> DeleteAsync(string userId, string credentialId, CancellationToken cancellationToken);
}

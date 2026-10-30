namespace CL.WebLogic.Auth.Users;

/// <summary>
/// Optional user-store capability: deleting a user. <see cref="Accounts.WebAccountService.DeleteAccountAsync"/> uses it
/// after revoking what WebLogic keeps (sessions, passkeys, recovery codes); the store removes the user, their
/// external-login links and whatever app data must go with the account.
/// </summary>
public interface IWebUserDeletionStore
{
    /// <summary>Deletes the user; false when there was no such user.</summary>
    Task<bool> DeleteAsync(string userId, CancellationToken cancellationToken);
}

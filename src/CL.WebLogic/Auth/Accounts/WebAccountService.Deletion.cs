using CL.WebLogic.Auth.Events;
using CL.WebLogic.Auth.Users;
using CodeLogic.Core.Results;

namespace CL.WebLogic.Auth.Accounts;

public sealed partial class WebAccountService
{
    /// <summary>
    /// Deletes an account: ends its sessions, removes its passkeys and recovery codes, then deletes the user through
    /// <see cref="IWebUserDeletionStore"/> (which also removes external-login links and app data). Sessions are ended
    /// again afterwards, so a request that was running during the delete cannot leave one behind. Publishes
    /// <see cref="UserDeletedEvent"/> (audited as <c>auth.account</c> / <c>deleted</c>; realtime connections close).
    /// Fails with <c>weblogic.auth.delete_unsupported</c> when the user store cannot delete, and
    /// <c>weblogic.auth.user_not_found</c> for an unknown id.
    /// </summary>
    /// <param name="userId">The user to delete (<see cref="WebUser.Id"/>).</param>
    /// <param name="byUserId">The administrator who deleted it, or null when the user deleted their own account.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    public async Task<Result> DeleteAccountAsync(string userId, string? byUserId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        if (_users is not IWebUserDeletionStore deletion)
            return Result.Failure(Error.Validation("weblogic.auth.delete_unsupported", "The user store cannot delete accounts."));
        if (await _users.FindByIdAsync(userId, cancellationToken).ConfigureAwait(false) is null)
            return Result.Failure(Error.NotFound("weblogic.auth.user_not_found", "The account does not exist."));

        await _sessions.DeleteForUserAsync(userId, cancellationToken).ConfigureAwait(false);
        foreach (var passkey in await _passkeys.ListAsync(userId, cancellationToken).ConfigureAwait(false))
            await _passkeys.DeleteAsync(userId, passkey.CredentialId, cancellationToken).ConfigureAwait(false);
        await _recoveryCodes.ReplaceAsync(userId, [], cancellationToken).ConfigureAwait(false);
        if (!await deletion.DeleteAsync(userId, cancellationToken).ConfigureAwait(false))
            return Result.Failure(Error.NotFound("weblogic.auth.user_not_found", "The account does not exist."));
        await _sessions.DeleteForUserAsync(userId, cancellationToken).ConfigureAwait(false);

        _context.Events.Publish(new UserDeletedEvent(userId, byUserId));
        return Result.Success();
    }
}

namespace CL.WebLogic.Auth.Users;

/// <summary>Links external identities (Steam, OpenID Connect…) to users.</summary>
public interface IWebUserExternalLoginStore
{
    /// <summary>The user linked to <paramref name="provider"/> / <paramref name="providerKey"/>, or null.</summary>
    Task<WebUser?> FindByExternalLoginAsync(string provider, string providerKey, CancellationToken cancellationToken);

    /// <summary>Links an external identity to a user.</summary>
    Task AddExternalLoginAsync(string userId, string provider, string providerKey, CancellationToken cancellationToken);

    /// <summary>
    /// Unlinks the user's identity at <paramref name="provider"/>; false when there was none. Stores written before
    /// 4.9.9 do not implement it and throw <see cref="NotSupportedException"/>.
    /// </summary>
    Task<bool> RemoveExternalLoginAsync(string userId, string provider, CancellationToken cancellationToken) =>
        throw new NotSupportedException($"{GetType().Name} does not support unlinking external logins.");
}

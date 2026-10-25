namespace CL.WebLogic.Auth.Users;

/// <summary>Looks users up by e-mail address (password reset, magic links).</summary>
public interface IWebUserEmailStore
{
    /// <summary>Finds a user by e-mail. Case-insensitive.</summary>
    Task<WebUser?> FindByEmailAsync(string email, CancellationToken cancellationToken);
}

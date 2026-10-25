using CL.WebLogic.Auth.Users;
using CodeLogic.Core.Results;

namespace CL.WebLogic.Auth.Passwords;

/// <summary>Decides whether a new password is acceptable.</summary>
public interface IWebPasswordPolicy
{
    /// <summary>Checks <paramref name="password"/> for <paramref name="user"/> (null while registering).</summary>
    Result Validate(string password, WebUser? user);
}

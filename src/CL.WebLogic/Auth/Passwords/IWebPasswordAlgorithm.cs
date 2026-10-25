namespace CL.WebLogic.Auth.Passwords;

/// <summary>
/// One password hashing algorithm, identified by the id in <c>$wl${id}$…</c>. Register extra
/// algorithms to verify hashes imported from older systems; they are upgraded on next sign-in.
/// </summary>
public interface IWebPasswordAlgorithm
{
    /// <summary>Id used in the hash string, e.g. <c>pbkdf2-sha256</c>.</summary>
    string Id { get; }

    /// <summary>Verifies; <paramref name="parameters"/> and <paramref name="payload"/> are the parts after the id.</summary>
    WebPasswordVerification Verify(string parameters, string payload, string password);
}

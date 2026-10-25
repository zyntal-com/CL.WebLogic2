namespace CL.WebLogic.Auth.Passwords;

/// <summary>Hashes and verifies passwords.</summary>
public interface IWebPasswordHasher
{
    /// <summary>Hashes <paramref name="password"/> with the current algorithm and parameters.</summary>
    string Hash(string password);

    /// <summary>Verifies <paramref name="password"/> against <paramref name="hash"/> in constant time.</summary>
    WebPasswordVerification Verify(string hash, string password);
}

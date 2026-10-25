namespace CL.WebLogic.Auth.Passwords;

/// <summary>Result of checking a password against a hash.</summary>
public enum WebPasswordVerification
{
    /// <summary>Wrong password (or unreadable hash).</summary>
    Failed,

    /// <summary>Correct.</summary>
    Success,

    /// <summary>Correct, but the hash uses old parameters or an old algorithm — store a new hash.</summary>
    SuccessRehashNeeded
}

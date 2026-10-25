namespace CL.WebLogic.Auth.TwoFactor;

/// <summary>Stores hashed two-factor recovery codes. Supplied by the app.</summary>
public interface IWebRecoveryCodeStore
{
    /// <summary>Replaces all codes of a user.</summary>
    Task ReplaceAsync(string userId, IReadOnlyList<string> codeHashes, CancellationToken cancellationToken);

    /// <summary>Removes the code atomically; false when it does not exist (already used).</summary>
    Task<bool> TryConsumeAsync(string userId, string codeHash, CancellationToken cancellationToken);

    /// <summary>Number of unused codes.</summary>
    Task<int> CountAsync(string userId, CancellationToken cancellationToken);
}

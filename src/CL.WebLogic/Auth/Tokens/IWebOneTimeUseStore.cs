namespace CL.WebLogic.Auth.Tokens;

/// <summary>
/// Remembers keys that were used once (TOTP time steps, account tokens) until they expire, so
/// they cannot be replayed. The default is per-node memory; clustered apps supply a shared store.
/// </summary>
public interface IWebOneTimeUseStore
{
    /// <summary>Marks <paramref name="key"/> as used; returns false when it was already used.</summary>
    ValueTask<bool> TryUseAsync(string key, DateTimeOffset expiresAt, CancellationToken cancellationToken);
}

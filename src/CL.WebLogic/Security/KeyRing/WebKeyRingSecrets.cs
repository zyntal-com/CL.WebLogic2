using System.Security.Cryptography;
using System.Text;

namespace CL.WebLogic.Security.KeyRing;

/// <summary>
/// The secrets that encrypt the DataProtection key ring at rest (see
/// <c>ProtectKeysWithSecret</c>). The first secret encrypts new keys; all of them decrypt, so a
/// secret can be rotated by moving the old one to the previous list. Each secret is stretched with
/// HKDF-SHA256 into an AES-256 key identified by a short fingerprint.
/// </summary>
public sealed class WebKeyRingSecrets
{
    /// <summary>Associated data and HKDF info; changing it makes stored keys unreadable.</summary>
    internal static readonly byte[] Purpose = Encoding.ASCII.GetBytes("CL.WebLogic.KeyRing.v1");

    private readonly Dictionary<string, byte[]> _keys = new(StringComparer.Ordinal);

    /// <summary>Creates the set; <paramref name="current"/> encrypts, all decrypt.</summary>
    public WebKeyRingSecrets(string current, IEnumerable<string>? previous = null)
    {
        (CurrentId, CurrentKey) = Derive(current, nameof(current));
        _keys[CurrentId] = CurrentKey;
        foreach (var secret in previous ?? [])
        {
            var (id, key) = Derive(secret, nameof(previous));
            _keys.TryAdd(id, key);
        }
    }

    /// <summary>Fingerprint of the encrypting key (stored next to each encrypted key).</summary>
    public string CurrentId { get; }

    internal byte[] CurrentKey { get; }

    internal bool TryGetKey(string id, out byte[] key) => _keys.TryGetValue(id, out key!);

    /// <summary>
    /// Reads a secret from an environment variable: base64 of at least 32 bytes, or text of at
    /// least 32 characters. Throws when missing or too short (a startup error, by design).
    /// </summary>
    public static string FromEnvironment(string variable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(variable);
        var value = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"The DataProtection key-ring secret environment variable '{variable}' is not set.");
        return value.Trim();
    }

    private static (string Id, byte[] Key) Derive(string secret, string parameter)
    {
        var material = Material(secret) ?? throw new ArgumentException(
            "A key-ring secret must be base64 of at least 32 bytes or text of at least 32 characters (e.g. `openssl rand -base64 48`).", parameter);
        var key = HKDF.DeriveKey(HashAlgorithmName.SHA256, material, 32, salt: null, info: Purpose);
        var id = Convert.ToHexStringLower(SHA256.HashData(key).AsSpan(0, 8));
        CryptographicOperations.ZeroMemory(material);
        return (id, key);
    }

    private static byte[]? Material(string? secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
            return null;
        secret = secret.Trim();
        var buffer = new byte[secret.Length];
        if (Convert.TryFromBase64String(secret, buffer, out var written) && written >= 32)
            return buffer[..written];
        return secret.Length >= 32 ? Encoding.UTF8.GetBytes(secret) : null;
    }
}

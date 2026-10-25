using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CL.WebLogic.Configuration.Services;

namespace CL.WebLogic.Auth.Passwords;

/// <summary>
/// PBKDF2-HMAC-SHA256 (RFC 8018) with a 128-bit random salt and a 256-bit output, written as
/// <c>$wl$pbkdf2-sha256$v=1$i=600000$salt$hash</c> (base64url). With a configured pepper the
/// password is first keyed with HMAC-SHA256 and the id becomes <c>pbkdf2-sha256-p</c>. Hashes
/// with fewer iterations than configured, or from another registered algorithm, verify as
/// <see cref="WebPasswordVerification.SuccessRehashNeeded"/>.
/// </summary>
public sealed class Pbkdf2PasswordHasher : IWebPasswordHasher
{
    private const string Prefix = "$wl$";
    private const string PlainId = "pbkdf2-sha256";
    private const string PepperedId = "pbkdf2-sha256-p";
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    private readonly WebLogicConfigHolder _config;
    private readonly Dictionary<string, IWebPasswordAlgorithm> _legacy;

    /// <summary>Creates the hasher; <paramref name="legacyAlgorithms"/> verify (and trigger upgrade of) older hash formats.</summary>
    public Pbkdf2PasswordHasher(WebLogicConfigHolder config, IEnumerable<IWebPasswordAlgorithm>? legacyAlgorithms = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _legacy = (legacyAlgorithms ?? []).ToDictionary(a => a.Id, StringComparer.Ordinal);
    }

    /// <inheritdoc/>
    public string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        var passwords = _config.Current.Auth.Passwords;
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var peppered = passwords.Pepper.Length > 0;
        var hash = Derive(password, salt, passwords.Iterations, peppered ? passwords.Pepper : null);
        return string.Create(CultureInfo.InvariantCulture,
            $"{Prefix}{(peppered ? PepperedId : PlainId)}$v=1$i={passwords.Iterations}${Base64Url.EncodeToString(salt)}${Base64Url.EncodeToString(hash)}");
    }

    /// <inheritdoc/>
    public WebPasswordVerification Verify(string hash, string password)
    {
        if (string.IsNullOrEmpty(hash) || password is null || !hash.StartsWith(Prefix, StringComparison.Ordinal))
            return WebPasswordVerification.Failed;

        var parts = hash.Substring(Prefix.Length).Split('$');
        var id = parts[0];
        if (id is not (PlainId or PepperedId))
        {
            return parts.Length >= 3 && _legacy.TryGetValue(id, out var algorithm)
                && algorithm.Verify(parts[1], string.Join('$', parts.Skip(2)), password) != WebPasswordVerification.Failed
                ? WebPasswordVerification.SuccessRehashNeeded
                : WebPasswordVerification.Failed;
        }

        if (parts.Length != 5 || parts[1] != "v=1" || !parts[2].StartsWith("i=", StringComparison.Ordinal)
            || !int.TryParse(parts[2].AsSpan(2), NumberStyles.None, CultureInfo.InvariantCulture, out var iterations) || iterations < 1)
            return WebPasswordVerification.Failed;

        byte[] salt, expected;
        try
        {
            salt = Base64Url.DecodeFromChars(parts[3]);
            expected = Base64Url.DecodeFromChars(parts[4]);
        }
        catch (FormatException)
        {
            return WebPasswordVerification.Failed;
        }

        var passwords = _config.Current.Auth.Passwords;
        var pepper = id == PepperedId ? passwords.Pepper : null;
        if (id == PepperedId && string.IsNullOrEmpty(pepper))
            return WebPasswordVerification.Failed;

        var actual = Derive(password, salt, iterations, pepper, expected.Length);
        if (!CryptographicOperations.FixedTimeEquals(actual, expected))
            return WebPasswordVerification.Failed;

        var current = iterations >= passwords.Iterations && (id == PepperedId) == (passwords.Pepper.Length > 0);
        return current ? WebPasswordVerification.Success : WebPasswordVerification.SuccessRehashNeeded;
    }

    private static byte[] Derive(string password, byte[] salt, int iterations, string? pepper, int length = HashBytes)
    {
        var input = Encoding.UTF8.GetBytes(password);
        if (pepper is not null)
            input = HMACSHA256.HashData(Encoding.UTF8.GetBytes(pepper), input);
        return Rfc2898DeriveBytes.Pbkdf2(input, salt, iterations, HashAlgorithmName.SHA256, length);
    }
}

using System.Security.Cryptography;
using System.Text;

namespace CL.WebLogic.Auth.TwoFactor;

/// <summary>Generates, normalizes and hashes recovery codes (<c>XXXXX-XXXXX</c>, 50 bits each).</summary>
public static class WebRecoveryCodes
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no 0/O, 1/I

    /// <summary>Creates <paramref name="count"/> codes.</summary>
    public static IReadOnlyList<string> Generate(int count)
    {
        var codes = new string[count];
        for (var i = 0; i < count; i++)
        {
            var chars = RandomNumberGenerator.GetItems<char>(Alphabet, 10);
            codes[i] = new string(chars, 0, 5) + "-" + new string(chars, 5, 5);
        }
        return codes;
    }

    /// <summary>Upper-cases and strips separators and spaces.</summary>
    public static string Normalize(string code) =>
        new((code ?? string.Empty).Where(char.IsAsciiLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    /// <summary>The stored hash without a pepper: HMAC-SHA256 keyed by the user id, over the normalized code.</summary>
    public static string Hash(string userId, string code) => Hash(userId, code, pepper: null);

    /// <summary>
    /// The stored hash: HMAC-SHA256 over the normalized code, keyed by the user id and, when set, the
    /// password pepper. With a pepper a leaked code table cannot be brute-forced offline (the user id
    /// alone is public and a code has only about 50 bits).
    /// </summary>
    public static string Hash(string userId, string code, string? pepper) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(string.IsNullOrEmpty(pepper) ? userId : pepper + "\0" + userId),
            Encoding.UTF8.GetBytes(Normalize(code))));
}

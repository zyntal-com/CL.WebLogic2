using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace CL.WebLogic.Security.Sessions;

/// <summary>Session token generation and hashing.</summary>
public static class WebSessionTokens
{
    /// <summary>A new 256-bit random token (base64url, 43 characters).</summary>
    public static string NewToken() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    /// <summary>A new 256-bit random secret (base64url).</summary>
    public static string NewSecret() => NewToken();

    /// <summary>Lower-case hex SHA-256 of a token — the store key.</summary>
    public static string Hash(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(token)));

    /// <summary>True when <paramref name="token"/> has the shape of a token we issued.</summary>
    public static bool IsWellFormed(string? token) =>
        token is { Length: 43 } && token.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
}

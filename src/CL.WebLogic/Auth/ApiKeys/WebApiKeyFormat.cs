using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace CL.WebLogic.Auth.ApiKeys;

/// <summary>The key format <c>wl_{prefix}_{secret}</c>: a 12-character lookup prefix and a 256-bit secret.</summary>
public static class WebApiKeyFormat
{
    private const string Lead = "wl_";
    private const string PrefixAlphabet = "abcdefghijkmnpqrstuvwxyz23456789";
    private const int PrefixLength = 12;
    private const int SecretLength = 43; // 32 bytes, base64url

    /// <summary>Creates a new key and its prefix.</summary>
    public static (string Key, string Prefix) Generate()
    {
        var prefix = new string(RandomNumberGenerator.GetItems<char>(PrefixAlphabet, PrefixLength));
        return (Lead + prefix + "_" + Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32)), prefix);
    }

    /// <summary>True when <paramref name="value"/> looks like a WebLogic API key.</summary>
    public static bool LooksLikeKey(string? value) => value is not null && value.StartsWith(Lead, StringComparison.Ordinal);

    /// <summary>Extracts the prefix of a well-formed key.</summary>
    public static bool TryParse(string? key, out string prefix)
    {
        prefix = string.Empty;
        if (key is null || key.Length != Lead.Length + PrefixLength + 1 + SecretLength || !LooksLikeKey(key) || key[Lead.Length + PrefixLength] != '_')
            return false;
        var candidate = key.Substring(Lead.Length, PrefixLength);
        if (!candidate.All(c => PrefixAlphabet.Contains(c, StringComparison.Ordinal)))
            return false;
        if (!key.AsSpan(Lead.Length + PrefixLength + 1).ContainsAnyExcept(Base64UrlChars))
        {
            prefix = candidate;
            return true;
        }
        return false;
    }

    /// <summary>Lower-case hex SHA-256 of the full key.</summary>
    public static string Hash(string key) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

    private static readonly System.Buffers.SearchValues<char> Base64UrlChars =
        System.Buffers.SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_");
}

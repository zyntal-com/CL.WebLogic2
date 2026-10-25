using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;

namespace CL.WebLogic.IntegrationTests.Auth;

/// <summary>RFC 6238 codes for any time step (independent of the implementation under test).</summary>
internal static class TotpCodes
{
    public static long CurrentStep => DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;

    public static string At(string base32Secret, long step)
    {
        Span<byte> counter = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counter, step);
#pragma warning disable CA5350 // RFC 6238 TOTP is defined over HMAC-SHA1
        var hash = HMACSHA1.HashData(Base32(base32Secret), counter);
#pragma warning restore CA5350
        var offset = hash[^1] & 0x0f;
        var value = ((hash[offset] & 0x7f) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (value % 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
    }

    private static byte[] Base32(string value)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bytes = new List<byte>();
        int buffer = 0, bits = 0;
        foreach (var c in value.TrimEnd('=').ToUpperInvariant())
        {
            buffer = (buffer << 5) | alphabet.IndexOf(c, StringComparison.Ordinal);
            bits += 5;
            if (bits >= 8)
            {
                bytes.Add((byte)(buffer >> (bits - 8)));
                bits -= 8;
            }
        }
        return [.. bytes];
    }
}

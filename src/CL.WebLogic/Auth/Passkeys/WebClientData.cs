using System.Security.Cryptography;
using System.Text.Json;

namespace CL.WebLogic.Auth.Passkeys;

/// <summary>The browser-written <c>clientDataJSON</c> of a ceremony (WebAuthn §5.8.1).</summary>
public sealed class WebClientData
{
    private WebClientData(byte[] raw)
    {
        Raw = raw;
        Hash = SHA256.HashData(raw);
    }

    /// <summary>The bytes as sent.</summary>
    public byte[] Raw { get; }

    /// <summary>SHA-256 of <see cref="Raw"/> (what the authenticator signs).</summary>
    public byte[] Hash { get; }

    /// <summary><c>webauthn.create</c> or <c>webauthn.get</c>.</summary>
    public string Type { get; private init; } = "";

    /// <summary>The challenge, base64url as the browser wrote it.</summary>
    public string Challenge { get; private init; } = "";

    /// <summary>The page origin.</summary>
    public string Origin { get; private init; } = "";

    /// <summary>True when the ceremony ran in a cross-origin iframe.</summary>
    public bool CrossOrigin { get; private init; }

    /// <summary>Parses client data; null when it is not the expected JSON object.</summary>
    public static WebClientData? TryParse(byte[] raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        if (raw.Length is 0 or > 16 * 1024)
            return null;
        try
        {
            using var document = JsonDocument.Parse(raw);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;
            return new WebClientData(raw)
            {
                Type = Text(root, "type"),
                Challenge = Text(root, "challenge"),
                Origin = Text(root, "origin"),
                CrossOrigin = root.TryGetProperty("crossOrigin", out var cross) && cross.ValueKind == JsonValueKind.True
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : "";
}

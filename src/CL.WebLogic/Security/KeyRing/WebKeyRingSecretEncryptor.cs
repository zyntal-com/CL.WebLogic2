using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;

namespace CL.WebLogic.Security.KeyRing;

/// <summary>
/// Encrypts DataProtection keys before they are stored (AES-256-GCM with a key from
/// <see cref="WebKeyRingSecrets"/>). Pairs with <see cref="WebKeyRingSecretDecryptor"/>.
/// </summary>
public sealed class WebKeyRingSecretEncryptor : IXmlEncryptor
{
    internal const string ElementName = "webLogicEncryptedKey";
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly WebKeyRingSecrets _secrets;

    /// <summary>Creates the encryptor.</summary>
    public WebKeyRingSecretEncryptor(WebKeyRingSecrets secrets) =>
        _secrets = secrets ?? throw new ArgumentNullException(nameof(secrets));

    /// <inheritdoc/>
    public EncryptedXmlInfo Encrypt(XElement plaintextElement)
    {
        ArgumentNullException.ThrowIfNull(plaintextElement);
        var plaintext = Encoding.UTF8.GetBytes(plaintextElement.ToString(SaveOptions.DisableFormatting));
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];
        try
        {
            using var aes = new AesGcm(_secrets.CurrentKey, TagSize);
            aes.Encrypt(nonce, plaintext, ciphertext, tag, WebKeyRingSecrets.Purpose);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }

        var element = new XElement(ElementName,
            new XAttribute("version", "1"),
            new XAttribute("keyId", _secrets.CurrentId),
            new XComment(" Encrypted with a CL.WebLogic key-ring secret (AES-256-GCM). "),
            new XElement("nonce", Convert.ToBase64String(nonce)),
            new XElement("tag", Convert.ToBase64String(tag)),
            new XElement("value", Convert.ToBase64String(ciphertext)));
        return new EncryptedXmlInfo(element, typeof(WebKeyRingSecretDecryptor));
    }

    /// <summary>Decrypts an element made by <see cref="Encrypt"/>.</summary>
    internal static XElement Decrypt(WebKeyRingSecrets secrets, XElement encrypted)
    {
        ArgumentNullException.ThrowIfNull(encrypted);
        if (encrypted.Name != ElementName || (string?)encrypted.Attribute("version") != "1")
            throw new CryptographicException("Not a CL.WebLogic encrypted key (unknown element or version).");
        var keyId = (string?)encrypted.Attribute("keyId") ?? "";
        if (!secrets.TryGetKey(keyId, out var key))
            throw new CryptographicException($"The key ring was encrypted with a secret ({keyId}) that is not configured; add it to the previous secrets.");

        var nonce = Convert.FromBase64String((string?)encrypted.Element("nonce") ?? "");
        var tag = Convert.FromBase64String((string?)encrypted.Element("tag") ?? "");
        var ciphertext = Convert.FromBase64String((string?)encrypted.Element("value") ?? "");
        if (nonce.Length != NonceSize || tag.Length != TagSize)
            throw new CryptographicException("The encrypted key is malformed.");

        var plaintext = new byte[ciphertext.Length];
        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(nonce, ciphertext, tag, plaintext, WebKeyRingSecrets.Purpose);
            return XElement.Parse(Encoding.UTF8.GetString(plaintext));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }
}

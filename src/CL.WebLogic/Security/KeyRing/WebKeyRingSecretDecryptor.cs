using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.DependencyInjection;

namespace CL.WebLogic.Security.KeyRing;

/// <summary>
/// Decrypts keys stored by <see cref="WebKeyRingSecretEncryptor"/>. DataProtection creates it by
/// type name (recorded in each key file) with the application's service provider.
/// </summary>
public sealed class WebKeyRingSecretDecryptor : IXmlDecryptor
{
    private readonly WebKeyRingSecrets _secrets;

    /// <summary>Created by DataProtection; resolves <see cref="WebKeyRingSecrets"/>.</summary>
    public WebKeyRingSecretDecryptor(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _secrets = services.GetService<WebKeyRingSecrets>() ?? throw new InvalidOperationException(
            "The DataProtection key ring is encrypted with a CL.WebLogic secret, but none is configured (call ProtectKeysWithSecret).");
    }

    /// <inheritdoc/>
    public XElement Decrypt(XElement encryptedElement) => WebKeyRingSecretEncryptor.Decrypt(_secrets, encryptedElement);
}

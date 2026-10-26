using System.Security.Cryptography;

namespace CL.WebLogic.Testing.Services;

/// <summary>A credential held by a <see cref="WebTestAuthenticator"/>.</summary>
public sealed class WebTestCredential : IDisposable
{
    internal WebTestCredential(byte[] id, string rpId, byte[] userHandle, int algorithm, AsymmetricAlgorithm key)
    {
        Id = id;
        RpId = rpId;
        UserHandle = userHandle;
        Algorithm = algorithm;
        Key = key;
    }

    /// <summary>The credential id.</summary>
    public byte[] Id { get; }

    /// <summary>The RP id it is scoped to.</summary>
    public string RpId { get; }

    /// <summary>The user handle the site gave at registration.</summary>
    public byte[] UserHandle { get; }

    /// <summary>The COSE algorithm.</summary>
    public int Algorithm { get; }

    /// <summary>The private key (ECDsa or RSA).</summary>
    public AsymmetricAlgorithm Key { get; }

    /// <summary>The signature counter; set it lower to simulate a cloned authenticator.</summary>
    public uint SignCount { get; set; }

    /// <inheritdoc/>
    public void Dispose() => Key.Dispose();
}

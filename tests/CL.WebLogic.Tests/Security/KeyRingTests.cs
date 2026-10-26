using System.Security.Cryptography;
using System.Xml.Linq;
using CL.Storage.Configuration;
using CL.Storage.Providers.Local;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Security.KeyRing;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.DependencyInjection;

namespace CL.WebLogic.Tests.Security;

/// <summary>DataProtection keys in CL.Storage and encrypted with a key-ring secret.</summary>
public sealed class KeyRingTests : IAsyncDisposable
{
    private const string Secret = "a-key-ring-secret-that-is-long-enough-0123456789";
    private const string OtherSecret = "another-key-ring-secret-also-long-enough-987654";

    private readonly string _root = Directory.CreateTempSubdirectory("wl-keyring-").FullName;
    private readonly LocalStorageBackend _storage;

    public KeyRingTests() =>
        _storage = new LocalStorageBackend("keys", new LocalConnectionConfig { RootPath = _root });

    [Fact]
    public void Keys_are_stored_one_file_each_and_never_overwritten()
    {
        var repository = new CodeLogicStorageXmlRepository(() => _storage, "keys", "/dp/keys/");
        Assert.Empty(repository.GetAllElements()); // the folder does not exist yet

        repository.StoreElement(new XElement("key", new XAttribute("id", "1")), "key-6f1b");
        repository.StoreElement(new XElement("key", new XAttribute("id", "2")), "../../evil name");

        var files = Directory.GetFiles(Path.Combine(_root, "dp", "keys")).Select(Path.GetFileName).ToList();
        Assert.Contains("key-6f1b.xml", files);
        Assert.Contains("key-evilname.xml", files);
        Assert.Equal(["1", "2"], repository.GetAllElements().Select(e => (string)e.Attribute("id")!).Order());
        Assert.Throws<InvalidOperationException>(() => repository.StoreElement(new XElement("key"), "6f1b"));
    }

    [Fact]
    public void Paths_with_dot_segments_are_refused() =>
        Assert.Throws<ArgumentException>(() => new CodeLogicStorageXmlRepository(() => _storage, "keys", "dp/../../etc"));

    [Fact]
    public void Two_nodes_sharing_the_storage_read_each_others_payloads_and_keys_are_encrypted_at_rest()
    {
        var protectedText = Node(new WebKeyRingSecrets(Secret)).CreateProtector("test").Protect("hello cluster");
        Assert.Equal("hello cluster", Node(new WebKeyRingSecrets(Secret)).CreateProtector("test").Unprotect(protectedText));

        var stored = File.ReadAllText(Directory.GetFiles(Path.Combine(_root, "dp")).Single());
        Assert.Contains("webLogicEncryptedKey", stored);
        Assert.DoesNotContain("<masterKey", stored);

        // Rotation: a new current secret still reads keys made with the previous one.
        var rotated = Node(new WebKeyRingSecrets(OtherSecret, [Secret]));
        Assert.Equal("hello cluster", rotated.CreateProtector("test").Unprotect(protectedText));
        // Without the old secret the key ring cannot be read.
        Assert.ThrowsAny<CryptographicException>(() => Node(new WebKeyRingSecrets(OtherSecret)).CreateProtector("test").Unprotect(protectedText));
    }

    [Fact]
    public void Tampered_or_foreign_elements_do_not_decrypt()
    {
        var secrets = new WebKeyRingSecrets(Secret);
        var encrypted = new WebKeyRingSecretEncryptor(secrets).Encrypt(new XElement("masterKey", "secret")).EncryptedElement;
        Assert.Equal("secret", WebKeyRingSecretEncryptor.Decrypt(secrets, encrypted).Value);

        var value = encrypted.Element("value")!;
        var bytes = Convert.FromBase64String(value.Value);
        bytes[0] ^= 1;
        value.Value = Convert.ToBase64String(bytes);
        Assert.ThrowsAny<CryptographicException>(() => WebKeyRingSecretEncryptor.Decrypt(secrets, encrypted));
        Assert.ThrowsAny<CryptographicException>(() => WebKeyRingSecretEncryptor.Decrypt(secrets, new XElement("other")));
    }

    [Theory]
    [InlineData("short")]
    [InlineData("c2hvcnQ=")] // base64 of 5 bytes
    [InlineData("")]
    public void Weak_secrets_are_refused(string secret) =>
        Assert.Throws<ArgumentException>(() => new WebKeyRingSecrets(secret));

    [Fact]
    public void A_base64_secret_and_its_text_form_are_different_keys()
    {
        var base64 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        Assert.NotEqual(new WebKeyRingSecrets(base64).CurrentId, new WebKeyRingSecrets(Secret).CurrentId);
        Assert.Equal(new WebKeyRingSecrets(base64).CurrentId, new WebKeyRingSecrets(base64).CurrentId);
    }

    public async ValueTask DisposeAsync()
    {
        await _storage.DisposeAsync();
        Directory.Delete(_root, recursive: true);
    }

    /// <summary>A separate "node": its own container, the shared storage folder and the given secrets.</summary>
    private IDataProtectionProvider Node(WebKeyRingSecrets secrets)
    {
        var services = new ServiceCollection();
        services.AddDataProtection().SetApplicationName("keyring-tests").ProtectKeysWithSecret(secrets);
        services.Configure<KeyManagementOptions>(o => o.XmlRepository = new CodeLogicStorageXmlRepository(() => _storage, "keys", "dp"));
        return services.BuildServiceProvider().GetRequiredService<IDataProtectionProvider>();
    }
}

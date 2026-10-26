using CL.Storage;
using CL.Storage.Abstractions;
using CL.WebLogic.Security.KeyRing;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CL.WebLogic.Hosting.Services;

/// <summary>DataProtection key-ring options backed by CodeLogic libraries.</summary>
public static class WebLogicDataProtectionBuilderExtensions
{
    /// <summary>
    /// Stores the key ring in the CL.Storage connection <paramref name="connectionId"/> under
    /// <paramref name="path"/>, so all nodes share it (cluster checks accept it). CL.Storage must be
    /// loaded before the first request. Combine with <c>SetApplicationName</c> and, for keys at
    /// rest, <c>ProtectKeysWithSecret</c> or <c>ProtectKeysWithCertificate</c>.
    /// </summary>
    public static IDataProtectionBuilder PersistKeysToCodeLogicStorage(this IDataProtectionBuilder builder, string connectionId, string path = "dataprotection-keys")
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        _ = new CodeLogicStorageXmlRepository(() => throw new InvalidOperationException(), connectionId, path); // validates the path now
        builder.Services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(services => new ConfigureOptions<KeyManagementOptions>(options =>
            options.XmlRepository = new CodeLogicStorageXmlRepository(() => Resolve(services, connectionId), connectionId, path)));
        return builder;
    }

    /// <summary>
    /// Encrypts the key ring at rest with a secret from the environment variable
    /// <paramref name="environmentVariable"/> (AES-256-GCM; base64 of at least 32 bytes, or text of at
    /// least 32 characters). Secrets in <paramref name="previousEnvironmentVariables"/> still decrypt
    /// older keys, for rotation. A missing variable fails at startup.
    /// </summary>
    public static IDataProtectionBuilder ProtectKeysWithSecret(this IDataProtectionBuilder builder, string environmentVariable, params string[] previousEnvironmentVariables)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var secrets = new WebKeyRingSecrets(
            WebKeyRingSecrets.FromEnvironment(environmentVariable),
            previousEnvironmentVariables.Select(WebKeyRingSecrets.FromEnvironment));
        return builder.ProtectKeysWithSecret(secrets);
    }

    /// <summary>Encrypts the key ring at rest with <paramref name="secrets"/> (e.g. read from a secret store).</summary>
    public static IDataProtectionBuilder ProtectKeysWithSecret(this IDataProtectionBuilder builder, WebKeyRingSecrets secrets)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(secrets);
        builder.Services.AddSingleton(secrets);
        builder.Services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(new ConfigureOptions<KeyManagementOptions>(options =>
            options.XmlEncryptor = new WebKeyRingSecretEncryptor(secrets)));
        return builder;
    }

    private static IStorageService Resolve(IServiceProvider services, string connectionId)
    {
        var storage = services.GetService<IWebLibraryResolver>()?.GetLibrary<StorageLibrary>()
            ?? throw new InvalidOperationException("The DataProtection key ring is in CL.Storage, but CL.Storage is not loaded. Load StorageLibrary before WebLogic.");
        return storage.TryGetStorage(connectionId, out var service) && service is not null
            ? service
            : throw new InvalidOperationException($"The DataProtection key ring storage connection '{connectionId}' does not exist or is disabled.");
    }
}

using System.Text;
using System.Xml.Linq;
using CL.Storage.Abstractions;
using CL.Storage.Models;
using Microsoft.AspNetCore.DataProtection.Repositories;

namespace CL.WebLogic.Security.KeyRing;

/// <summary>
/// Keeps the DataProtection key ring in a CL.Storage connection (S3, Azure, FTP, a shared disk…) so
/// every node reads the same keys. One file per key, <c>{path}/key-{id}.xml</c>, written once and
/// never overwritten. Register with
/// <c>services.AddDataProtection().PersistKeysToCodeLogicStorage(connectionId, path)</c>.
/// </summary>
/// <remarks>
/// <see cref="IXmlRepository"/> is synchronous, so storage calls block. DataProtection caches the
/// key ring (it reloads about once a day and when a key is created), so this happens rarely. Storage
/// failures throw instead of returning an empty ring: an empty ring would mint a new key and make
/// every existing cookie and token unreadable until the storage came back.
/// </remarks>
public sealed class CodeLogicStorageXmlRepository : IXmlRepository
{
    private readonly Func<IStorageService> _storage;

    /// <summary>Creates the repository. <paramref name="storage"/> is resolved on each use.</summary>
    public CodeLogicStorageXmlRepository(Func<IStorageService> storage, string connectionId, string path)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        ConnectionId = connectionId;
        Path = NormalizePath(path);
    }

    /// <summary>The CL.Storage connection the keys live in.</summary>
    public string ConnectionId { get; }

    /// <summary>The folder inside the connection.</summary>
    public string Path { get; }

    /// <inheritdoc/>
    public IReadOnlyCollection<XElement> GetAllElements() =>
        Task.Run(() => GetAllElementsAsync(CancellationToken.None)).GetAwaiter().GetResult();

    /// <inheritdoc/>
    public void StoreElement(XElement element, string friendlyName)
    {
        ArgumentNullException.ThrowIfNull(element);
        Task.Run(() => StoreElementAsync(element, friendlyName, CancellationToken.None)).GetAwaiter().GetResult();
    }

    /// <summary>Reads every key file.</summary>
    public async Task<IReadOnlyCollection<XElement>> GetAllElementsAsync(CancellationToken cancellationToken)
    {
        var storage = _storage();
        var exists = await storage.ExistsAsync(Path, cancellationToken).ConfigureAwait(false);
        if (exists.IsSuccess && !exists.Value)
            return [];

        var elements = new List<XElement>();
        string? continuation = null;
        do
        {
            var page = await storage.ListAsync(Path, new StorageListOptions { ContinuationToken = continuation }, cancellationToken).ConfigureAwait(false);
            if (page.IsFailure)
                throw Failure("list", page.Error?.Message);
            foreach (var item in page.Value!.Items)
            {
                if (item.ItemType != StorageItemType.File || !item.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    continue;
                var bytes = await storage.DownloadBytesAsync(string.IsNullOrEmpty(item.Path) ? Combine(item.Name) : item.Path, cancellationToken: cancellationToken).ConfigureAwait(false);
                if (bytes.IsFailure)
                    throw Failure("read " + item.Name, bytes.Error?.Message);
                using var stream = new MemoryStream(bytes.Value!);
                elements.Add(XElement.Load(stream));
            }
            continuation = page.Value.ContinuationToken;
        }
        while (!string.IsNullOrEmpty(continuation));
        return elements;
    }

    /// <summary>Writes one key file (never overwrites).</summary>
    public async Task StoreElementAsync(XElement element, string friendlyName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(element);
        var name = SafeName(friendlyName);
        var content = Encoding.UTF8.GetBytes(element.ToString(SaveOptions.DisableFormatting));
        var result = await _storage().UploadBytesAsync(Combine("key-" + name + ".xml"), content,
            new StorageUploadOptions { Overwrite = false, CreateParents = true, ContentType = "application/xml" }, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
            throw Failure("write key " + name, result.Error?.Message);
    }

    private string Combine(string name) => Path.Length == 0 ? name : Path + "/" + name;

    private InvalidOperationException Failure(string action, string? message) =>
        new($"DataProtection key ring: could not {action} in storage '{ConnectionId}' at '{Path}': {message ?? "unknown error"}");

    private static string SafeName(string? friendlyName)
    {
        var name = new string((friendlyName ?? "").Where(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_').ToArray());
        if (name.StartsWith("key-", StringComparison.OrdinalIgnoreCase))
            name = name[4..];
        return name.Length is > 0 and <= 100 ? name : Guid.NewGuid().ToString("D");
    }

    private static string NormalizePath(string? path)
    {
        var trimmed = (path ?? "").Replace('\\', '/').Trim('/');
        if (trimmed.Split('/').Any(segment => segment is ".." or "."))
            throw new ArgumentException("The key ring path must not contain '.' or '..' segments.", nameof(path));
        return trimmed;
    }
}

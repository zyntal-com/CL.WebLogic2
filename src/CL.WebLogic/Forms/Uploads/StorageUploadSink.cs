using System.Security.Cryptography;
using CL.Storage;
using CL.Storage.Models;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Hosting.Services;
using CodeLogic.Core.Results;

namespace CL.WebLogic.Forms.Uploads;

/// <summary>
/// Saves uploads to the CL.Storage connection <c>Forms.UploadStorageConnection</c>, under
/// <c>Forms.UploadFolder/{folder}/{yyyy}/{MM}/{random}{ext}</c> — never under the client's file name.
/// </summary>
public sealed class StorageUploadSink : IWebUploadSink
{
    private readonly IWebLibraryResolver _libraries;
    private readonly WebLogicConfigHolder _config;
    private readonly TimeProvider _time;

    /// <summary>Creates the sink.</summary>
    public StorageUploadSink(IWebLibraryResolver libraries, WebLogicConfigHolder config, TimeProvider? time = null)
    {
        _libraries = libraries ?? throw new ArgumentNullException(nameof(libraries));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _time = time ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public async Task<Result<WebStoredFile>> SaveAsync(WebUploadedFile file, string? folder, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);
        var config = _config.Current.Forms;
        var storage = _libraries.GetLibrary<StorageLibrary>();
        if (storage is null || !storage.TryGetStorage(config.UploadStorageConnection, out var service) || service is null)
            return Result<WebStoredFile>.Failure(Error.Unavailable("weblogic.forms.storage_unavailable", $"Storage connection '{config.UploadStorageConnection}' is not available."));

        var now = _time.GetUtcNow();
        var path = string.Join('/', new[] { config.UploadFolder, Clean(folder), now.ToString("yyyy", System.Globalization.CultureInfo.InvariantCulture),
            now.ToString("MM", System.Globalization.CultureInfo.InvariantCulture), Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16)) + file.Extension }
            .Where(p => !string.IsNullOrEmpty(p)));

        await using var stream = file.OpenReadStream();
        var result = await service.UploadAsync(path, stream, new StorageUploadOptions { ContentType = file.DetectedType, CreateParents = true }, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Result<WebStoredFile>.Success(new WebStoredFile(result.Value!.Path, file.FileName, file.Length, file.DetectedType))
            : Result<WebStoredFile>.Failure(result.Error!);
    }

    private static string? Clean(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
            return null;
        var parts = folder.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => p is not ("." or ".."))
            .Select(p => new string([.. p.Where(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_')]))
            .Where(p => p.Length > 0);
        return string.Join('/', parts);
    }
}

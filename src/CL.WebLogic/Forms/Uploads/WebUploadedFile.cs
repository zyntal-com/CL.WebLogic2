using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Forms.Uploads;

/// <summary>
/// An uploaded file after the upload checks. <see cref="FileName"/> is cleaned for display —
/// never use it as a storage path (the upload sink generates names).
/// </summary>
public sealed class WebUploadedFile
{
    private readonly IFormFile _file;

    internal WebUploadedFile(IFormFile file, string fileName, string detectedType, int? width, int? height)
    {
        _file = file;
        FileName = fileName;
        DetectedType = detectedType;
        Width = width;
        Height = height;
    }

    /// <summary>The form field.</summary>
    public string FieldName => _file.Name;

    /// <summary>Cleaned original file name (no path, no control characters).</summary>
    public string FileName { get; }

    /// <summary>Lower-case extension including the dot.</summary>
    public string Extension => Path.GetExtension(FileName).ToLowerInvariant();

    /// <summary>The content type the browser claimed (not trusted).</summary>
    public string ClaimedContentType => _file.ContentType ?? "";

    /// <summary>The content type detected from the file's bytes.</summary>
    public string DetectedType { get; }

    /// <summary>Size in bytes.</summary>
    public long Length => _file.Length;

    /// <summary>Image width, for images.</summary>
    public int? Width { get; }

    /// <summary>Image height, for images.</summary>
    public int? Height { get; }

    /// <summary>Opens the content.</summary>
    public Stream OpenReadStream() => _file.OpenReadStream();
}

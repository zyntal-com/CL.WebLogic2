namespace CL.WebLogic.Forms.Models;

/// <summary>Upload limits of a file field.</summary>
public sealed record WebFileRules
{
    /// <summary>Maximum bytes per file (null = <c>Forms.MaxFileBytes</c>).</summary>
    public long? MaxBytes { get; init; }

    /// <summary>Allowed extensions (empty = <c>Forms.AllowedExtensions</c>).</summary>
    public IReadOnlyList<string> Extensions { get; init; } = [];

    /// <summary>Maximum number of files (multiple uploads).</summary>
    public int MaxFiles { get; init; } = 1;

    /// <summary>Only images (PNG, JPEG, GIF, WebP, BMP — checked by content).</summary>
    public bool ImagesOnly { get; init; }

    /// <summary>Maximum image width.</summary>
    public int? MaxWidth { get; init; }

    /// <summary>Maximum image height.</summary>
    public int? MaxHeight { get; init; }
}

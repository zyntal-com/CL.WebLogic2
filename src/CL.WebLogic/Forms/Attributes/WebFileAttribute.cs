namespace CL.WebLogic.Forms.Attributes;

/// <summary>Upload rules of a file field.</summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class WebFileAttribute : Attribute
{
    /// <summary>Maximum bytes per file (0 = <c>Forms.MaxFileBytes</c>).</summary>
    public long MaxBytes { get; set; }

    /// <summary>Allowed extensions, comma separated (e.g. <c>.png,.jpg</c>).</summary>
    public string? Extensions { get; set; }

    /// <summary>Maximum number of files.</summary>
    public int MaxFiles { get; set; } = 1;

    /// <summary>Only images (checked by content).</summary>
    public bool ImagesOnly { get; set; }

    /// <summary>Maximum image width (0 = any).</summary>
    public int MaxWidth { get; set; }

    /// <summary>Maximum image height (0 = any).</summary>
    public int MaxHeight { get; set; }
}

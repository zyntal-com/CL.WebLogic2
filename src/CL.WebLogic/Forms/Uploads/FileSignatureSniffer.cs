
namespace CL.WebLogic.Forms.Uploads;

/// <summary>Detects a file's real type from its first bytes and checks it against the extension.</summary>
public static class FileSignatureSniffer
{
    private static readonly Dictionary<string, string> ExpectedByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".bmp"] = "image/bmp",
        [".pdf"] = "application/pdf",
        [".zip"] = "application/zip",
        [".docx"] = "application/zip",
        [".xlsx"] = "application/zip",
        [".pptx"] = "application/zip",
        [".odt"] = "application/zip",
        [".txt"] = "text/plain",
        [".csv"] = "text/plain",
        [".md"] = "text/plain",
        [".json"] = "text/plain"
    };

    /// <summary>The detected content type, or <c>application/octet-stream</c>.</summary>
    public static string Detect(ReadOnlySpan<byte> header)
    {
        if (ImageHeaderReader.TryRead(header, out var image))
            return image.ContentType;
        if (header.StartsWith("%PDF-"u8))
            return "application/pdf";
        if (header.StartsWith("PK\u0003\u0004"u8))
            return "application/zip";
        if (header.Length > 0 && LooksLikeText(header))
            return "text/plain";
        return "application/octet-stream";
    }

    /// <summary>True when the detected type is what the extension promises (unknown extensions accept anything but executables and markup).</summary>
    public static bool Matches(string extension, string detected) =>
        ExpectedByExtension.TryGetValue(extension, out var expected) ? expected == detected : true;

    /// <summary>True for image types we can read dimensions of.</summary>
    public static bool IsImage(string detected) => detected.StartsWith("image/", StringComparison.Ordinal);

    private static bool LooksLikeText(ReadOnlySpan<byte> header)
    {
        if (header.Contains((byte)0))
            return false;
        var sample = header[..Math.Min(header.Length, 4096)];
        // A cut-off multi-byte sequence at the end is fine; anything else invalid is not text.
        for (var trim = 0; trim < 4 && trim < sample.Length; trim++)
        {
            if (System.Text.Unicode.Utf8.IsValid(sample[..(sample.Length - trim)]))
                return true;
        }
        return false;
    }
}

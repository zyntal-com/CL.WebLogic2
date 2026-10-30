namespace CL.WebLogic.Forms.Uploads;

/// <summary>Detects a file's real type from its first bytes and checks it against the extension.</summary>
public static class FileSignatureSniffer
{
    /// <summary>Detected type of programs (PE, ELF, Mach-O, scripts starting with <c>#!</c>).</summary>
    public const string Executable = "application/x-executable";

    /// <summary>Detected type of text that starts like markup (HTML, SVG, XML).</summary>
    public const string Markup = "text/html";

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

    /// <summary>
    /// The detected content type: an image type, <c>application/pdf</c>, <c>application/zip</c>,
    /// <see cref="Executable"/>, <see cref="Markup"/>, <c>text/plain</c> or <c>application/octet-stream</c>.
    /// </summary>
    public static string Detect(ReadOnlySpan<byte> header)
    {
        if (ImageHeaderReader.TryRead(header, out var image))
            return image.ContentType;
        if (header.StartsWith("%PDF-"u8))
            return "application/pdf";
        if (header.StartsWith("PK\u0003\u0004"u8))
            return "application/zip";
        if (IsExecutable(header))
            return Executable;
        if (header.Length > 0 && LooksLikeText(header))
            return StartsLikeMarkup(header) ? Markup : "text/plain";
        return "application/octet-stream";
    }

    /// <summary>
    /// True when the detected type is what the extension promises. Text extensions also accept text
    /// that starts with markup (it is stored and served as text). Unknown extensions accept anything
    /// but executables and markup, which a server or CDN choosing the type by name could run.
    /// </summary>
    public static bool Matches(string extension, string detected) =>
        ExpectedByExtension.TryGetValue(extension, out var expected)
            ? expected == detected || (expected == "text/plain" && detected == Markup)
            : detected is not (Executable or Markup);

    /// <summary>True for image types we can read dimensions of.</summary>
    public static bool IsImage(string detected) => detected.StartsWith("image/", StringComparison.Ordinal);

    // PE, ELF, Mach-O (both byte orders, 32/64-bit), universal binaries, scripts.
    private static readonly byte[][] ExecutableSignatures =
    [
        "MZ"u8.ToArray(), "\u007FELF"u8.ToArray(), [0xFE, 0xED, 0xFA, 0xCE], [0xFE, 0xED, 0xFA, 0xCF],
        [0xCE, 0xFA, 0xED, 0xFE], [0xCF, 0xFA, 0xED, 0xFE], [0xCA, 0xFE, 0xBA, 0xBE], "#!"u8.ToArray()
    ];

    private static bool IsExecutable(ReadOnlySpan<byte> header)
    {
        foreach (var signature in ExecutableSignatures)
        {
            if (header.StartsWith(signature))
                return true;
        }
        return false;
    }

    private static bool StartsLikeMarkup(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith("﻿"u8))
            header = header[3..];
        var start = 0;
        while (start < header.Length && header[start] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
            start++;
        return start < header.Length && header[start] == (byte)'<';
    }

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

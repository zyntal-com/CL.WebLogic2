using System.IO.Compression;
using CodeLogic.Core.Configuration;

namespace CL.WebLogic.Configuration.Models;

/// <summary>
/// Response compression (Brotli, gzip) with ASP.NET Core's response-compression middleware.
/// Off by default. Responses that may hold personal data (signed-in users, a loaded session, a
/// CSRF token, cookies being set) are never compressed, so secrets cannot leak through the
/// compressed size (BREACH); a route can opt in with <c>AllowPersonalCompression()</c>.
/// </summary>
public sealed class CompressionConfig
{
    /// <summary>Compress responses for clients that send <c>Accept-Encoding: br</c> or <c>gzip</c>.</summary>
    [ConfigField(Label = "Enable Compression",
        Description = "Brotli/gzip for anonymous, non-personal responses. Signed-in, session, CSRF and cookie-setting responses are never compressed (BREACH).",
        Group = "Compression", Order = 1)]
    public bool Enabled { get; set; }

    /// <summary>Compression level for Brotli and gzip. <c>Fastest</c> keeps CPU cost low for dynamic pages.</summary>
    [ConfigField(Label = "Level", Description = "Fastest, Optimal or SmallestSize.", Group = "Compression", Order = 2)]
    public CompressionLevel Level { get; set; } = CompressionLevel.Fastest;

    /// <summary>
    /// Extra content types to compress, in addition to ASP.NET Core's defaults (HTML, CSS,
    /// JavaScript, JSON, XML, plain text), <c>image/svg+xml</c> and <c>application/problem+json</c>.
    /// </summary>
    [ConfigField(Label = "Extra MIME Types", Description = "Additional content types to compress, e.g. application/manifest+json.",
        Group = "Compression", Order = 3, Collapsed = true)]
    public List<string> MimeTypes { get; set; } = [];
}

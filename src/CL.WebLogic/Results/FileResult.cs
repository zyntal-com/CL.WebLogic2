using CL.WebLogic.Runtime.Models;
using Microsoft.Net.Http.Headers;

namespace CL.WebLogic.Results;

/// <summary>
/// A streamed file with HTTP range, <c>If-Modified-Since</c> and <c>If-None-Match</c> support
/// (delegated to the ASP.NET stream result). The stream is disposed after the response.
/// </summary>
public sealed class FileResult : WebResult
{
    /// <summary>Creates the result.</summary>
    public FileResult(Stream stream, string contentType, string? downloadName = null, DateTimeOffset? lastModified = null)
    {
        Stream = stream ?? throw new ArgumentNullException(nameof(stream));
        ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType;
        DownloadName = downloadName;
        LastModified = lastModified;
    }

    /// <summary>The content stream.</summary>
    public Stream Stream { get; }

    /// <summary>The content type.</summary>
    public string ContentType { get; }

    /// <summary>When set, the browser is told to download the file under this name.</summary>
    public string? DownloadName { get; }

    /// <summary>Last-modified timestamp for conditional requests.</summary>
    public DateTimeOffset? LastModified { get; }

    /// <summary>Entity tag (quoted) for conditional requests.</summary>
    public string? ETag { get; init; }

    /// <inheritdoc/>
    public override Task ExecuteAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        foreach (var (name, value) in Headers)
            request.HttpContext.Response.Headers[name] = value;

        var tag = string.IsNullOrEmpty(ETag) ? null : new EntityTagHeaderValue(ETag);
        return Microsoft.AspNetCore.Http.Results
            .Stream(Stream, ContentType, DownloadName, LastModified, tag, enableRangeProcessing: Stream.CanSeek)
            .ExecuteAsync(request.HttpContext);
    }
}

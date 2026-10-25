using CL.WebLogic.Runtime.Models;

namespace CL.WebLogic.Results;

/// <summary>A raw byte response with an explicit content type.</summary>
public sealed class BytesResult : WebResult
{
    /// <summary>Creates the result.</summary>
    public BytesResult(byte[] data, string contentType)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
        ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType;
    }

    /// <summary>The bytes to write.</summary>
    public byte[] Data { get; }

    /// <summary>The response content type.</summary>
    public string ContentType { get; }

    /// <inheritdoc/>
    public override async Task ExecuteAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        var response = request.HttpContext.Response;
        ApplyStatusAndHeaders(response);
        response.ContentType = ContentType;
        response.ContentLength = Data.Length;
        await response.Body.WriteAsync(Data, cancellationToken).ConfigureAwait(false);
    }
}

using CL.WebLogic.Client.Models;
using CL.WebLogic.Results;
using CL.WebLogic.Runtime.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace CL.WebLogic.Client.Services;

/// <summary>Serves a client file: precompressed by Accept-Encoding, ETag / 304, immutable when fingerprinted.</summary>
internal sealed class ClientAssetResult : WebResult
{
    private readonly ClientAsset _asset;
    private readonly bool _fingerprinted;

    public ClientAssetResult(ClientAsset asset, bool fingerprinted)
    {
        _asset = asset;
        _fingerprinted = fingerprinted;
    }

    public override async Task ExecuteAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        var response = request.HttpContext.Response;
        ApplyStatusAndHeaders(response);
        response.Headers.CacheControl = _fingerprinted ? "public, max-age=31536000, immutable" : "no-cache";
        response.Headers.ETag = _asset.ETag;
        response.Headers.Vary = HeaderNames.AcceptEncoding;
        response.Headers.XContentTypeOptions = "nosniff";
        response.Headers["Cross-Origin-Resource-Policy"] = "same-origin";
        response.ContentType = _asset.ContentType;

        var ifNoneMatch = request.HttpContext.Request.Headers.IfNoneMatch.ToString();
        if (ifNoneMatch.Length > 0 && (ifNoneMatch == "*" || ifNoneMatch.Split(',').Any(tag => tag.Trim() == _asset.ETag)))
        {
            response.StatusCode = StatusCodes.Status304NotModified;
            return;
        }

        var accept = request.HttpContext.Request.Headers.AcceptEncoding.ToString();
        var body = _asset.Content;
        if (_asset.Brotli is not null && Accepts(accept, "br"))
        {
            body = _asset.Brotli;
            response.Headers.ContentEncoding = "br";
        }
        else if (_asset.Gzip is not null && Accepts(accept, "gzip"))
        {
            body = _asset.Gzip;
            response.Headers.ContentEncoding = "gzip";
        }

        response.ContentLength = body.Length;
        if (!HttpMethods.IsHead(request.Method))
            await response.Body.WriteAsync(body, cancellationToken).ConfigureAwait(false);
    }

    private static bool Accepts(string header, string coding)
    {
        foreach (var part in header.Split(','))
        {
            var pieces = part.Split(';');
            if (!string.Equals(pieces[0].Trim(), coding, StringComparison.OrdinalIgnoreCase))
                continue;
            return !pieces.Skip(1).Any(p => p.Replace(" ", "", StringComparison.Ordinal) is "q=0" or "q=0.0" or "q=0.00" or "q=0.000");
        }
        return false;
    }
}

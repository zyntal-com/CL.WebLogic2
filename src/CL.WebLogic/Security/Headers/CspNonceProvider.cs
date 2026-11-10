using System.Buffers.Text;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Security.Headers;

/// <summary>Generates a 128-bit random nonce per request and stores it in <c>HttpContext.Items</c>.</summary>
public sealed class CspNonceProvider : ICspNonceProvider
{
    private static readonly object ItemKey = new();

    /// <inheritdoc/>
    public string GetNonce(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        if (httpContext.Items.TryGetValue(ItemKey, out var existing) && existing is string nonce)
            return nonce;

        nonce = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16)); // URL-safe: no characters to encode in HTML or headers
        httpContext.Items[ItemKey] = nonce;
        return nonce;
    }

    /// <summary>
    /// Internal response header that carries a page's nonce into the output cache. Response hardening removes it
    /// before the response leaves, so clients never see it.
    /// </summary>
    internal const string CachedNonceHeader = "X-WebLogic-Cached-Nonce";

    /// <summary>
    /// Makes <paramref name="nonce"/> this request's nonce: a page served from the output cache keeps the nonce in
    /// its HTML, so the Content-Security-Policy header has to name that one. Ignored when it is not a nonce.
    /// </summary>
    internal static void Adopt(HttpContext httpContext, string? nonce)
    {
        if (nonce is { Length: > 0 and <= 128 } && Base64Url.IsValid(nonce))
            httpContext.Items[ItemKey] = nonce;
    }

    /// <summary>The nonce when one was issued for this request, else null (used by response hardening).</summary>
    public static string? Peek(HttpContext httpContext) =>
        httpContext.Items.TryGetValue(ItemKey, out var existing) ? existing as string : null;
}

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

    /// <summary>The nonce when one was issued for this request, else null (used by response hardening).</summary>
    public static string? Peek(HttpContext httpContext) =>
        httpContext.Items.TryGetValue(ItemKey, out var existing) ? existing as string : null;
}

using System.Text;
using System.Text.Json;
using CL.WebLogic.Runtime.Models;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Runtime.Services;

/// <summary>Bounded readers for request bodies. Never buffers more than the route's limit.</summary>
internal static class WebRequestBodyReader
{
    /// <summary>Default body limit when a route sets none (1 MiB).</summary>
    public const long DefaultMaxBodyBytes = 1024 * 1024;

    /// <summary>Reads the body as UTF-8 text, failing with <see cref="WebRequestTooLargeException"/> above <paramref name="limit"/>.</summary>
    public static async Task<string> ReadStringAsync(HttpRequest request, long limit, CancellationToken cancellationToken)
    {
        if (request.ContentLength > limit)
            throw new WebRequestTooLargeException(limit);

        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await request.Body.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > limit)
                throw new WebRequestTooLargeException(limit);
            buffer.Write(chunk, 0, read);
        }

        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    /// <summary>Deserializes a JSON body within the limit. Returns default for an empty body.</summary>
    public static async Task<T?> ReadJsonAsync<T>(HttpRequest request, long limit, JsonSerializerOptions options, CancellationToken cancellationToken)
    {
        var text = await ReadStringAsync(request, limit, cancellationToken).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(text) ? default : JsonSerializer.Deserialize<T>(text, options);
    }
}

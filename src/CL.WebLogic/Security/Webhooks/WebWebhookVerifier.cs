using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CL.WebLogic.Runtime.Models;
using CL.WebLogic.Runtime.Services;
using CodeLogic.Core.Results;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Security.Webhooks;

/// <summary>
/// Verifies HMAC-SHA256 webhook signatures for routes marked <c>AllowCrossSitePost()</c>, which
/// must authenticate the sender themselves. Comparison is constant-time, several secrets can be
/// active at once (rotation) and an optional signed timestamp limits replays.
/// <para>
/// Error codes: <c>weblogic.webhook.missing_signature</c>, <c>weblogic.webhook.bad_signature</c>,
/// <c>weblogic.webhook.bad_timestamp</c>, <c>weblogic.webhook.expired</c>, <c>weblogic.webhook.too_large</c>.
/// Secrets and signatures are never logged or put into error messages.
/// </para>
/// </summary>
public sealed class WebWebhookVerifier
{
    private readonly TimeProvider _time;

    /// <summary>Creates the verifier.</summary>
    public WebWebhookVerifier(TimeProvider? time = null) => _time = time ?? TimeProvider.System;

    /// <summary>
    /// Reads the raw body (bounded by <see cref="WebWebhookOptions.MaxBodyBytes"/> and the route's limit) and
    /// verifies its signature. On success returns the body bytes; the request body is buffered and rewound,
    /// so <c>ReadJsonAsync</c> and friends still work afterwards.
    /// </summary>
    public async Task<Result<byte[]>> VerifyAsync(WebRequestContext request, WebWebhookOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(options);
        options.EnsureValid();

        var limit = Math.Min(options.MaxBodyBytes, request.Route.MaxBodyBytes ?? WebRequestBodyReader.DefaultMaxBodyBytes);
        var body = await ReadBodyAsync(request.HttpContext.Request, limit, cancellationToken).ConfigureAwait(false);
        if (body is null)
            return Result<byte[]>.Failure(Error.Validation("weblogic.webhook.too_large", "The webhook body is too large."));

        var timestamp = options.TimestampHeader is null ? null : request.GetHeader(options.TimestampHeader);
        var verified = Verify(body, request.GetHeader(options.SignatureHeader), timestamp, options);
        return verified.IsFailure ? Result<byte[]>.Failure(verified.Error!) : Result<byte[]>.Success(body);
    }

    /// <summary>
    /// Verifies <paramref name="body"/> against the signature header value (and the separate timestamp
    /// header value, when <see cref="WebWebhookOptions.TimestampHeader"/> is used).
    /// </summary>
    public Result Verify(ReadOnlySpan<byte> body, string? signatureHeader, string? timestampHeader, WebWebhookOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.EnsureValid();
        if (string.IsNullOrWhiteSpace(signatureHeader) || signatureHeader.Length > 4096)
            return Fail("weblogic.webhook.missing_signature", "The webhook signature is missing or malformed.");

        var (timestamp, candidates) = Parse(signatureHeader, timestampHeader, options);
        if (candidates.Count == 0)
            return Fail("weblogic.webhook.missing_signature", "The webhook signature is missing or malformed.");

        var prefix = Array.Empty<byte>();
        if (options.UsesTimestamp)
        {
            if (!long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) || seconds > 253402300799)
                return Fail("weblogic.webhook.bad_timestamp", "The webhook timestamp is missing or invalid.");
            var age = _time.GetUtcNow() - DateTimeOffset.FromUnixTimeSeconds(seconds);
            if (age.Duration() > options.Tolerance)
                return Fail("weblogic.webhook.expired", "The webhook timestamp is outside the allowed window.");
            prefix = Encoding.UTF8.GetBytes(options.SignedTimestampFormat.Replace("{timestamp}", timestamp, StringComparison.Ordinal));
        }

        var matched = false;
        foreach (var secret in options.Secrets)
        {
            using var hmac = IncrementalHash.CreateHMAC(HashAlgorithmName.SHA256, Encoding.UTF8.GetBytes(secret));
            hmac.AppendData(prefix);
            hmac.AppendData(body);
            var expected = hmac.GetHashAndReset();
            foreach (var candidate in candidates)
                matched |= CryptographicOperations.FixedTimeEquals(expected, candidate); // no short-circuit: same work for every secret
        }

        return matched ? Result.Success() : Fail("weblogic.webhook.bad_signature", "The webhook signature does not match.");
    }

    private static (string? Timestamp, List<byte[]> Candidates) Parse(string header, string? timestampHeader, WebWebhookOptions options)
    {
        var candidates = new List<byte[]>();
        if (options.TimestampKey is null)
        {
            if (Decode(Strip(header.Trim(), options.SignaturePrefix), options.Encoding) is { } single)
                candidates.Add(single);
            return (timestampHeader?.Trim(), candidates);
        }

        string? timestamp = null;
        foreach (var part in header.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var equals = part.IndexOf('=', StringComparison.Ordinal);
            if (equals <= 0)
                continue;
            var key = part[..equals];
            var value = part[(equals + 1)..];
            if (string.Equals(key, options.TimestampKey, StringComparison.Ordinal))
                timestamp ??= value;
            else if (string.Equals(key, options.SignatureKey, StringComparison.Ordinal) && candidates.Count < 8
                && Decode(Strip(value, options.SignaturePrefix), options.Encoding) is { } decoded)
                candidates.Add(decoded);
        }

        return (timestamp, candidates);
    }

    private static string Strip(string value, string? prefix) =>
        !string.IsNullOrEmpty(prefix) && value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? value[prefix.Length..] : value;

    private static byte[]? Decode(string value, WebWebhookSignatureEncoding encoding)
    {
        if (value.Length == 0)
            return null;
        if (encoding == WebWebhookSignatureEncoding.Base64)
        {
            var buffer = new byte[value.Length];
            return Convert.TryFromBase64String(value, buffer, out var written) ? buffer[..written] : null;
        }

        return value.Length % 2 == 0 && value.All(char.IsAsciiHexDigit) ? Convert.FromHexString(value) : null;
    }

    private static async Task<byte[]?> ReadBodyAsync(HttpRequest request, long limit, CancellationToken cancellationToken)
    {
        if (request.ContentLength > limit)
            return null;

        request.EnableBuffering();
        request.Body.Position = 0;
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await request.Body.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > limit)
                return null;
            buffer.Write(chunk, 0, read);
        }

        request.Body.Position = 0;
        return buffer.ToArray();
    }

    private static Result Fail(string code, string message) => Result.Failure(Error.Unauthorized(code, message));
}

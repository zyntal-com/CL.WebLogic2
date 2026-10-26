using System.Text.Json;
using CL.WebLogic.Runtime.Models;
using CL.WebLogic.Runtime.Services;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Results;

/// <summary>
/// The response of a WebLogic route. Concrete results live in their own files; the static
/// factories below are the usual way to create one (<c>WebResult.Json(payload)</c>).
/// </summary>
public abstract class WebResult
{
    private Dictionary<string, string>? _headers;

    /// <summary>HTTP status code.</summary>
    public int StatusCode { get; init; } = StatusCodes.Status200OK;

    /// <summary>Extra response headers. Security headers set by the pipeline cannot be overridden here.</summary>
    public IReadOnlyDictionary<string, string> Headers =>
        _headers ?? (IReadOnlyDictionary<string, string>)new Dictionary<string, string>();

    /// <summary>Adds a response header and returns this result.</summary>
    public WebResult WithHeader(string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        (_headers ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase))[name] = value;
        return this;
    }

    /// <summary>Writes the result to the response.</summary>
    public abstract Task ExecuteAsync(WebRequestContext request, CancellationToken cancellationToken);

    /// <summary>Sets the status code and extra headers. Call first from <see cref="ExecuteAsync"/>.</summary>
    protected void ApplyStatusAndHeaders(HttpResponse response)
    {
        response.StatusCode = StatusCode;
        if (_headers is null) return;
        foreach (var (name, value) in _headers)
            response.Headers[name] = value;
    }

    // ── Factories ─────────────────────────────────────────────────────────────

    /// <summary>A theme template rendered with its layout, e.g. <c>WebResult.Page("templates/home", model)</c>.</summary>
    public static PageResult Page(string templatePath, object? model = null, WebPageMeta? meta = null, int statusCode = StatusCodes.Status200OK) =>
        new(templatePath, model, meta, fragment: false) { StatusCode = statusCode };

    /// <summary>A theme template rendered without its layout, for partial page updates.</summary>
    public static PageResult Fragment(string templatePath, object? model = null, int statusCode = StatusCodes.Status200OK) =>
        new(templatePath, model, null, fragment: true) { StatusCode = statusCode };

    /// <summary>An HTML document or fragment. The string is written as-is — encode user data first.</summary>
    public static HtmlResult Html(string html, int statusCode = StatusCodes.Status200OK) => new(html) { StatusCode = statusCode };

    /// <summary>Plain UTF-8 text.</summary>
    public static TextResult Text(string text, int statusCode = StatusCodes.Status200OK) => new(text) { StatusCode = statusCode };

    /// <summary>A JSON payload serialized with the route contributor's options (or <paramref name="options"/>).</summary>
    public static JsonResult Json(object? payload, int statusCode = StatusCodes.Status200OK, JsonSerializerOptions? options = null) =>
        new(payload, options) { StatusCode = statusCode };

    /// <summary>Raw bytes with a content type.</summary>
    public static BytesResult Bytes(byte[] data, string contentType, int statusCode = StatusCodes.Status200OK) =>
        new(data, contentType) { StatusCode = statusCode };

    /// <summary>
    /// A redirect to a local path. Throws when <paramref name="localUrl"/> is not local
    /// (see <see cref="WebUrl.IsLocal"/>) — use <see cref="ExternalRedirect"/> for other origins.
    /// </summary>
    public static RedirectResult Redirect(string localUrl, bool permanent = false, bool preserveMethod = false)
    {
        if (!WebUrl.IsLocal(localUrl))
            throw new ArgumentException($"'{localUrl}' is not a local URL. Use WebResult.ExternalRedirect for other origins.", nameof(localUrl));
        return new RedirectResult(localUrl, permanent, preserveMethod);
    }

    /// <summary>A redirect to an absolute http(s) URL on another origin. Never pass unvalidated user input.</summary>
    public static RedirectResult ExternalRedirect(Uri url, bool permanent = false, bool preserveMethod = false)
    {
        ArgumentNullException.ThrowIfNull(url);
        if (!url.IsAbsoluteUri || (url.Scheme != Uri.UriSchemeHttps && url.Scheme != Uri.UriSchemeHttp))
            throw new ArgumentException("External redirects must be absolute http(s) URLs.", nameof(url));
        return new RedirectResult(url.AbsoluteUri, permanent, preserveMethod);
    }

    /// <summary>A file streamed from <paramref name="stream"/> with range support. The stream is disposed after writing.</summary>
    public static FileResult File(Stream stream, string contentType, string? downloadName = null, DateTimeOffset? lastModified = null) =>
        new(stream, contentType, downloadName, lastModified);

    /// <summary>An RFC 7807 problem response.</summary>
    public static ProblemResult Problem(int statusCode, string? title = null, string? detail = null, string? type = null) =>
        new(statusCode, title, detail, type);

    /// <summary>An RFC 7807 validation problem (400) carrying per-field errors.</summary>
    public static ProblemResult ValidationProblem(IReadOnlyDictionary<string, string[]> errors, string? title = null) =>
        ProblemResult.Validation(errors, title);

    /// <summary>A 403 page telling a suspended user why and until when (see <see cref="AccountSuspendedResult"/>).</summary>
    public static AccountSuspendedResult AccountSuspended(Auth.Users.WebSuspension suspension) => new(suspension);

    /// <summary>An empty 204 response.</summary>
    public static StatusResult NoContent() => new() { StatusCode = StatusCodes.Status204NoContent };

    /// <summary>An empty response with <paramref name="statusCode"/>.</summary>
    public static StatusResult Status(int statusCode) => new() { StatusCode = statusCode };

    /// <summary>A JSON command list for the WebLogic client (toast, redirect, replace…).</summary>
    public static CommandsResult Commands(params WebCommand[] commands) => new(commands);

    /// <summary>A server-sent events stream.</summary>
    public static SseResult Sse(IAsyncEnumerable<WebSseEvent> events) => new(events);

    /// <summary>Wraps an ASP.NET <see cref="IResult"/> (e.g. <c>Results.File(...)</c>) as a WebLogic result.</summary>
    public static AspNetResult FromAspNet(IResult result) => new(result);
}

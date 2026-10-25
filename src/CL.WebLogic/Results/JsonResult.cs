using System.Text.Json;
using CL.WebLogic.Runtime.Models;

namespace CL.WebLogic.Results;

/// <summary>A JSON response, serialized with the route contributor's options unless overridden.</summary>
public sealed class JsonResult : WebResult
{
    private readonly JsonSerializerOptions? _options;

    /// <summary>Creates the result.</summary>
    public JsonResult(object? payload, JsonSerializerOptions? options = null)
    {
        Payload = payload;
        _options = options;
    }

    /// <summary>The value to serialize.</summary>
    public object? Payload { get; }

    /// <summary>Content type, <c>application/json</c> by default.</summary>
    public string ContentType { get; init; } = "application/json; charset=utf-8";

    /// <inheritdoc/>
    public override async Task ExecuteAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        var response = request.HttpContext.Response;
        ApplyStatusAndHeaders(response);
        response.ContentType = ContentType;
        await JsonSerializer.SerializeAsync(response.Body, Payload, Payload?.GetType() ?? typeof(object),
            _options ?? request.JsonOptions, cancellationToken).ConfigureAwait(false);
    }
}

using System.Text.Json;
using CL.WebLogic.Forms.Models;
using CL.WebLogic.Forms.Validation;
using CL.WebLogic.Runtime.Models;
using CL.WebLogic.Runtime.Services;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Forms.Services;

/// <summary>
/// Reads a JSON body into a model and validates it (<c>request.BindJsonAsync&lt;T&gt;()</c>).
/// Every client mistake is a <see cref="WebJsonBinding{T}"/>, never an exception.
/// </summary>
internal static class WebJsonBinder
{
    /// <summary>Binds and validates the body of <paramref name="request"/> within <paramref name="limit"/> bytes.</summary>
    public static async Task<WebJsonBinding<T>> BindAsync<T>(WebRequestContext request, long limit, WebJsonValidator validator, WebFormMessages messages, CancellationToken cancellationToken)
    {
        var http = request.HttpContext.Request;
        if (!http.HasJsonContentType())
            return WebJsonBinding<T>.Failure(StatusCodes.Status415UnsupportedMediaType, "weblogic.json.unsupported_media_type", "The request body must be JSON (Content-Type: application/json).");

        string text;
        try
        {
            text = await WebRequestBodyReader.ReadStringAsync(http, limit, cancellationToken).ConfigureAwait(false);
        }
        catch (WebRequestTooLargeException ex)
        {
            return WebJsonBinding<T>.Failure(StatusCodes.Status413PayloadTooLarge, "weblogic.json.too_large", ex.Message);
        }

        if (string.IsNullOrWhiteSpace(text))
            return WebJsonBinding<T>.Failure(StatusCodes.Status400BadRequest, "weblogic.json.empty", "A JSON request body is required.");

        T? model;
        try
        {
            model = JsonSerializer.Deserialize<T>(text, request.JsonOptions);
        }
        catch (JsonException ex)
        {
            // A value of the wrong type has a path: report it on that field. The exception text
            // (CLR type names, positions) is not sent to the client.
            var key = FieldKey(ex.Path);
            if (key is not null)
                return WebJsonBinding<T>.Invalid(default, new Dictionary<string, string[]>(StringComparer.Ordinal) { [key] = [messages.Format("invalid", WebJsonValidator.LabelOf(key))] });
            return WebJsonBinding<T>.Failure(StatusCodes.Status400BadRequest, "weblogic.json.malformed", "The request body is not valid JSON for this endpoint.");
        }

        if (model is null)
            return WebJsonBinding<T>.Failure(StatusCodes.Status400BadRequest, "weblogic.json.empty", "A JSON request body is required.");

        var errors = validator.Validate(model, request.JsonOptions, request.Services);
        return errors.Count == 0 ? WebJsonBinding<T>.Success(model) : WebJsonBinding<T>.Invalid(model, errors);
    }

    /// <summary>
    /// <c>$.items[0].name</c> → <c>items[0].name</c>; null for the root or a path that is not a
    /// plain member path (quoted names such as <c>$['a.b']</c>).
    /// </summary>
    internal static string? FieldKey(string? path)
    {
        if (string.IsNullOrEmpty(path) || path.Contains('\'', StringComparison.Ordinal))
            return null;
        if (path.StartsWith("$.", StringComparison.Ordinal))
            return path[2..];
        return path.StartsWith("$[", StringComparison.Ordinal) ? path[1..] : null;
    }
}

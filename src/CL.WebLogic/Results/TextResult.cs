using CL.WebLogic.Runtime.Models;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Results;

/// <summary>A plain UTF-8 text response.</summary>
public sealed class TextResult : WebResult
{
    /// <summary>Creates the result.</summary>
    public TextResult(string text) => Content = text ?? string.Empty;

    /// <summary>The text to write.</summary>
    public string Content { get; }

    /// <inheritdoc/>
    public override async Task ExecuteAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        var response = request.HttpContext.Response;
        ApplyStatusAndHeaders(response);
        response.ContentType = "text/plain; charset=utf-8";
        await response.WriteAsync(Content, cancellationToken).ConfigureAwait(false);
    }
}

using CL.WebLogic.Runtime.Models;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Results;

/// <summary>An HTML response. The content is written verbatim — encode any user data before building it.</summary>
public sealed class HtmlResult : WebResult
{
    /// <summary>Creates the result.</summary>
    public HtmlResult(string html) => Content = html ?? string.Empty;

    /// <summary>The HTML to write.</summary>
    public string Content { get; }

    /// <inheritdoc/>
    public override async Task ExecuteAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        var response = request.HttpContext.Response;
        ApplyStatusAndHeaders(response);
        response.ContentType = "text/html; charset=utf-8";
        await response.WriteAsync(Content, cancellationToken).ConfigureAwait(false);
    }
}

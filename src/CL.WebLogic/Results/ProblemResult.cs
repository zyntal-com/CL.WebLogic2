using System.Text.Json;
using CL.WebLogic.Runtime.Models;
using CL.WebLogic.Runtime.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace CL.WebLogic.Results;

/// <summary>An RFC 7807 <c>application/problem+json</c> response.</summary>
public sealed class ProblemResult : WebResult
{
    /// <summary>Creates a problem with the given status and optional title / detail / type.</summary>
    public ProblemResult(int statusCode, string? title = null, string? detail = null, string? type = null)
        : this(new ProblemDetails { Status = statusCode, Title = title, Detail = detail, Type = type })
    {
    }

    private ProblemResult(ProblemDetails problem)
    {
        Details = problem;
        StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
    }

    /// <summary>The problem document.</summary>
    public ProblemDetails Details { get; }

    /// <summary>A 400 validation problem with per-field error messages.</summary>
    public static ProblemResult Validation(IReadOnlyDictionary<string, string[]> errors, string? title = null) =>
        new(new HttpValidationProblemDetails(errors.ToDictionary(kv => kv.Key, kv => kv.Value))
        {
            Status = StatusCodes.Status400BadRequest,
            Title = title ?? "One or more fields are invalid."
        });

    /// <inheritdoc/>
    public override Task ExecuteAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        ApplyStatusAndHeaders(request.HttpContext.Response);
        return WriteAsync(request.HttpContext, Details, cancellationToken);
    }

    /// <summary>
    /// Writes <paramref name="problem"/> as <c>application/problem+json</c>, filling in title and
    /// <c>traceId</c> defaults. Also used by the exception boundary for API errors.
    /// </summary>
    public static async Task WriteAsync(HttpContext httpContext, ProblemDetails problem, CancellationToken cancellationToken)
    {
        problem.Status ??= httpContext.Response.StatusCode;
        problem.Title ??= ReasonPhrases.GetReasonPhrase(problem.Status.Value);
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = problem.Status.Value;
        httpContext.Response.ContentType = "application/problem+json; charset=utf-8";
        await JsonSerializer.SerializeAsync(httpContext.Response.Body, problem, problem.GetType(),
            WebJson.Default, cancellationToken).ConfigureAwait(false);
    }
}

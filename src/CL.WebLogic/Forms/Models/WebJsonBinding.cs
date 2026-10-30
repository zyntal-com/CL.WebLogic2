using CL.WebLogic.Results;
using CodeLogic.Core.Results;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Forms.Models;

/// <summary>
/// The outcome of <c>request.BindJsonAsync&lt;T&gt;()</c>: the model when the body was JSON, within
/// the route's size limit and valid by its DataAnnotations; otherwise the status, error and
/// per-field messages for <see cref="ToProblem"/>.
/// </summary>
public sealed class WebJsonBinding<T>
{
    private static readonly IReadOnlyDictionary<string, string[]> NoErrors = new Dictionary<string, string[]>(StringComparer.Ordinal);

    private WebJsonBinding(T? model, int statusCode, Error? error, IReadOnlyDictionary<string, string[]> errors)
    {
        Model = model;
        StatusCode = statusCode;
        Error = error;
        Errors = errors;
    }

    /// <summary>True when the body was read and the model is valid.</summary>
    public bool IsValid => Error is null;

    /// <summary>The bound model; set when <see cref="IsValid"/>, and also when only validation failed.</summary>
    public T? Model { get; }

    /// <summary>200 when valid; otherwise 400 (malformed or invalid), 413 (too large) or 415 (not JSON).</summary>
    public int StatusCode { get; }

    /// <summary>Why the binding failed (codes <c>weblogic.json.*</c>), or null when valid.</summary>
    public Error? Error { get; }

    /// <summary>Messages by JSON path (<c>name</c>, <c>address.city</c>, <c>items[0].name</c>; "" = model-level).</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    /// <summary>
    /// The RFC 7807 answer for a failed binding: a validation problem (400 with <c>errors</c>) when
    /// there are field messages, otherwise a problem with <see cref="StatusCode"/> and the error message.
    /// </summary>
    /// <exception cref="InvalidOperationException">The binding is valid.</exception>
    public ProblemResult ToProblem()
    {
        if (Error is null)
            throw new InvalidOperationException("The JSON binding is valid; there is no problem to report.");
        return Errors.Count > 0
            ? ProblemResult.Validation(Errors)
            : new ProblemResult(StatusCode, detail: Error.Message);
    }

    internal static WebJsonBinding<T> Success(T model) =>
        new(model, StatusCodes.Status200OK, null, NoErrors);

    internal static WebJsonBinding<T> Invalid(T? model, IReadOnlyDictionary<string, string[]> errors) =>
        new(model, StatusCodes.Status400BadRequest,
            Error.Validation("weblogic.json.invalid", "One or more fields are invalid."), errors);

    internal static WebJsonBinding<T> Failure(int statusCode, string code, string message) =>
        new(default, statusCode, Error.Validation(code, message), NoErrors);
}

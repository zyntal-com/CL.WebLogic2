using CodeLogic.Core.Results;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Results;

/// <summary>
/// Maps a CodeLogic <see cref="Error"/> to an HTTP response by its kind, so app stores and services
/// only pick the right factory (<c>Error.Conflict</c>, <c>Error.NotFound</c>…) and never have to
/// know WebLogic's codes. The switch is exhaustive over the closed <see cref="Error"/> hierarchy.
/// </summary>
public static class WebErrorStatus
{
    /// <summary>The detail shown for an <see cref="InternalError"/>; its own message is never sent.</summary>
    public const string InternalDetail = "An unexpected error occurred.";

    /// <summary>
    /// The HTTP status code for <paramref name="error"/>. <see langword="null"/> (the error of a
    /// <c>default</c> Result, which reports <c>IsFailure</c>) is treated as <see cref="Error.Empty"/>: internal.
    /// </summary>
    public static int For(Error? error) =>
        (error ?? Error.Empty) switch
        {
            ValidationError => StatusCodes.Status400BadRequest,
            UnauthorizedError => StatusCodes.Status401Unauthorized,
            NotFoundError => StatusCodes.Status404NotFound,
            ConflictError => StatusCodes.Status409Conflict,
            UnavailableError => StatusCodes.Status503ServiceUnavailable,
            TimeoutError => StatusCodes.Status504GatewayTimeout,
            InternalError => StatusCodes.Status500InternalServerError,
        };

    /// <summary>
    /// The detail that is safe to send for <paramref name="error"/>: its message, except for an
    /// <see cref="InternalError"/>, whose message may carry exception or infrastructure details.
    /// </summary>
    public static string Detail(Error? error) =>
        error is null or InternalError ? InternalDetail : error.Message;
}

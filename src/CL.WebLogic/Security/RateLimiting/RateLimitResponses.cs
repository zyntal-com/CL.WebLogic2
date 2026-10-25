using System.Globalization;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Results;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Security.RateLimiting;

/// <summary>429 responses and <c>RateLimit-*</c> headers.</summary>
public static class RateLimitResponses
{
    /// <summary>A 429 with <c>Retry-After</c> and <c>RateLimit-*</c> headers.</summary>
    public static WebResult TooManyRequests(HttpContext httpContext, RateLimitDecision decision)
    {
        WebResult result = DefaultErrorPageRenderer.WantsProblemJson(httpContext)
            ? WebResult.Problem(StatusCodes.Status429TooManyRequests, detail: "Too many requests. Try again later.")
            : WebResult.Status(StatusCodes.Status429TooManyRequests);
        result.WithHeader("Retry-After", Seconds(decision.RetryAfter));
        foreach (var (name, value) in Headers(decision))
            result.WithHeader(name, value);
        return result;
    }

    /// <summary>The informational <c>RateLimit-Limit/Remaining/Reset</c> headers.</summary>
    public static IEnumerable<(string Name, string Value)> Headers(RateLimitDecision decision)
    {
        yield return ("RateLimit-Limit", decision.Limit.ToString(CultureInfo.InvariantCulture));
        yield return ("RateLimit-Remaining", Math.Max(0, decision.Remaining).ToString(CultureInfo.InvariantCulture));
        yield return ("RateLimit-Reset", Seconds(decision.Reset));
    }

    private static string Seconds(TimeSpan value) =>
        Math.Max(1, (int)Math.Ceiling(value.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
}

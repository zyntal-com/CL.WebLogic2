using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Security.Antiforgery;

/// <summary>Issues and validates CSRF tokens.</summary>
public interface IWebCsrfTokens
{
    /// <summary>A fresh masked token for this request (also marks the response non-cacheable).</summary>
    string GetToken(HttpContext httpContext);

    /// <summary>True when <paramref name="token"/> was issued to this browser.</summary>
    bool Validate(HttpContext httpContext, string? token);
}

using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Security.Headers;

/// <summary>Supplies the per-request Content-Security-Policy nonce used by templates and response hardening.</summary>
public interface ICspNonceProvider
{
    /// <summary>The nonce for <paramref name="httpContext"/>; the same value for the whole request.</summary>
    string GetNonce(HttpContext httpContext);
}

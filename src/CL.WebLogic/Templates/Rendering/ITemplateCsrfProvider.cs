using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Templates.Rendering;

/// <summary>Supplies the CSRF token for <c>{csrf}</c>, <c>{csrf_meta}</c> and <c>{csrf_token}</c>.</summary>
public interface ITemplateCsrfProvider
{
    /// <summary>The token to embed for this request (masked per render), or empty when CSRF is unavailable.</summary>
    string GetToken(HttpContext httpContext);

    /// <summary>Name of the hidden form field the server reads the token from.</summary>
    string FieldName { get; }
}

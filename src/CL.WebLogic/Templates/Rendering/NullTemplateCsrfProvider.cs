using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Templates.Rendering;

/// <summary>Placeholder until the security pipeline registers the real provider: emits no token.</summary>
public sealed class NullTemplateCsrfProvider : ITemplateCsrfProvider
{
    /// <inheritdoc/>
    public string FieldName => "__wl_csrf";

    /// <inheritdoc/>
    public string GetToken(HttpContext httpContext) => string.Empty;
}

using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Templates.Rendering;

/// <summary>
/// Contributes values every template can read (e.g. <c>site_name</c>, navigation, feature flags).
/// Register any number; later registrations override earlier keys. Called once per render.
/// </summary>
public interface ITemplateGlobalsProvider
{
    /// <summary>Adds values to <paramref name="globals"/>.</summary>
    void Contribute(IDictionary<string, object?> globals, HttpContext? httpContext);
}

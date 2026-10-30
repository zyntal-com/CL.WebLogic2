using CL.WebLogic.Localization.Models;
using CL.WebLogic.Templates.Rendering;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Localization.Services;

/// <summary>
/// Adds <c>culture</c> (the resolved culture name, e.g. <c>sv-SE</c>) to the template globals, for
/// <c>&lt;html lang="{model:culture}"&gt;</c> and language switchers. Adds nothing when
/// localization is off.
/// </summary>
public sealed class CultureTemplateGlobalsProvider : ITemplateGlobalsProvider
{
    /// <inheritdoc/>
    public void Contribute(IDictionary<string, object?> globals, HttpContext? httpContext)
    {
        ArgumentNullException.ThrowIfNull(globals);
        if (WebRequestCulture.Get(httpContext) is { } culture)
            globals["culture"] = culture.Name;
    }
}

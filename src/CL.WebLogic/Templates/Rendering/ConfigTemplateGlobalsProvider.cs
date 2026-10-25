using CL.WebLogic.Configuration.Services;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Templates.Rendering;

/// <summary>Built-in globals from the WebLogic config: <c>site_name</c> and <c>public_base_url</c>.</summary>
public sealed class ConfigTemplateGlobalsProvider : ITemplateGlobalsProvider
{
    private readonly WebLogicConfigHolder _config;

    /// <summary>Creates the provider.</summary>
    public ConfigTemplateGlobalsProvider(WebLogicConfigHolder config) =>
        _config = config ?? throw new ArgumentNullException(nameof(config));

    /// <inheritdoc/>
    public void Contribute(IDictionary<string, object?> globals, HttpContext? httpContext)
    {
        var current = _config.Current;
        globals["site_name"] = current.SiteName;
        globals["public_base_url"] = current.PublicBaseUrl;
    }
}

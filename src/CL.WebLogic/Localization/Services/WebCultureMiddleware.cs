using System.Globalization;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Localization.Models;
using CL.WebLogic.Routing.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace CL.WebLogic.Localization.Services;

/// <summary>
/// Resolves the request culture once (<see cref="WebCultureResolver"/>), stores it as
/// <see cref="WebRequestCulture"/> and runs the rest of the pipeline with
/// <see cref="CultureInfo.CurrentUICulture"/> (and, with <c>Localization.SetCurrentCulture</c>,
/// <see cref="CultureInfo.CurrentCulture"/>) set to it. The cultures flow with the async
/// execution context and are restored afterwards. Responses other than static assets get
/// <c>Content-Language</c>, and <c>Vary: Accept-Language</c> when the header source is configured.
/// Does nothing when localization is off. Runs after routing, before endpoint security and the output cache.
/// </summary>
public sealed class WebCultureMiddleware
{
    private readonly RequestDelegate _next;
    private readonly WebCultureResolver _resolver;
    private readonly WebLogicConfigHolder _config;

    /// <summary>Creates the middleware.</summary>
    public WebCultureMiddleware(RequestDelegate next, WebCultureResolver resolver, WebLogicConfigHolder config)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>Runs the middleware.</summary>
    public Task InvokeAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        var resolved = _resolver.Resolve(httpContext);
        return resolved is null ? _next(httpContext) : InvokeLocalizedAsync(httpContext, resolved);
    }

    private async Task InvokeLocalizedAsync(HttpContext httpContext, WebRequestCulture resolved)
    {
        var config = _config.Current.Localization;
        WebRequestCulture.Set(httpContext, resolved);

        if (httpContext.GetEndpoint()?.Metadata.GetMetadata<WebRouteMetadata>()?.Kind != WebRouteKind.Asset)
        {
            var headers = httpContext.Response.Headers;
            headers.ContentLanguage = resolved.Name;
            if (config.Sources.Contains(WebCultureSources.Header, StringComparer.OrdinalIgnoreCase))
                headers.Append(HeaderNames.Vary, HeaderNames.AcceptLanguage);
        }

        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            if (config.SetCurrentCulture)
                CultureInfo.CurrentCulture = resolved.Culture;
            CultureInfo.CurrentUICulture = resolved.Culture;
            await _next(httpContext).ConfigureAwait(false);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }
}

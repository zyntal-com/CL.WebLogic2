using CL.WebLogic.Routing.Models;

namespace CL.WebLogic.Routing.Services;

/// <summary>Typed route helpers over <see cref="IWebRouteMapper"/>.</summary>
public static class WebRouteMapperExtensions
{
    /// <summary>Maps an HTML page. Defaults to GET.</summary>
    public static WebRouteBuilder MapPage(this IWebRouteMapper mapper, string pattern, WebRouteHandler handler, params string[] methods) =>
        mapper.Map(WebRouteKind.Page, pattern, handler, methods);

    /// <summary>Maps an API endpoint. Defaults to GET.</summary>
    public static WebRouteBuilder MapApi(this IWebRouteMapper mapper, string pattern, WebRouteHandler handler, params string[] methods) =>
        mapper.Map(WebRouteKind.Api, pattern, handler, methods);

    /// <summary>Maps an HTML fragment endpoint for client-side swaps. Defaults to GET.</summary>
    public static WebRouteBuilder MapFragment(this IWebRouteMapper mapper, string pattern, WebRouteHandler handler, params string[] methods) =>
        mapper.Map(WebRouteKind.Fragment, pattern, handler, methods);

    /// <summary>Maps a server-sent events stream (GET).</summary>
    public static WebRouteBuilder MapSse(this IWebRouteMapper mapper, string pattern, WebRouteHandler handler) =>
        mapper.Map(WebRouteKind.Sse, pattern, handler, "GET");

    /// <summary>
    /// Maps the catch-all route that runs when no other endpoint matched. Defaults to GET.
    /// Only one fallback may be registered across all contributors.
    /// </summary>
    public static WebRouteBuilder MapFallback(this IWebRouteMapper mapper, WebRouteHandler handler, params string[] methods) =>
        mapper.Map(WebRouteKind.Fallback, "{**path}", handler, methods).WithOrder(int.MaxValue);
}

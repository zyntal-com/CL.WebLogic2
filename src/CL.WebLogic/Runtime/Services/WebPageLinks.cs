using CL.WebLogic.Runtime.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.WebUtilities;

namespace CL.WebLogic.Runtime.Services;

/// <summary>
/// Builds pager links that keep the current query string (filters, sort, search) and replace only
/// the page parameter. Links are always local paths: the path is the request's own (or one you pass,
/// which must be local), keys and values are URL-encoded, and page 1 drops the parameter so each
/// page has one canonical URL.
/// </summary>
public static class WebPageLinks
{
    /// <summary>Default query parameter.</summary>
    public const string DefaultParameter = "page";

    /// <summary>The URL of <paramref name="page"/> for the current request (path base, path and other query values kept).</summary>
    public static string PageUrl(WebRequestContext request, int page, string parameter = DefaultParameter)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Compose(RequestPath(request), request.QueryString, page, parameter);
    }

    /// <summary>
    /// The URL of <paramref name="page"/> for <paramref name="path"/> (a decoded local path such as
    /// <c>/tags/c#</c>; it is encoded here) with <paramref name="queryString"/> kept.
    /// </summary>
    /// <exception cref="ArgumentException">The path is not a local path (see <see cref="WebUrl.IsLocal"/>).</exception>
    public static string PageUrl(string path, string? queryString, int page, string parameter = DefaultParameter) =>
        Compose(EncodePath(path), queryString, page, parameter);

    /// <summary>Previous/next/first/last and numbered links for the current request.</summary>
    /// <param name="request">The current request.</param>
    /// <param name="pagination">The list's pagination.</param>
    /// <param name="window">Numbered links shown on each side of the current page.</param>
    /// <param name="parameter">The query parameter that holds the page.</param>
    public static WebPagerLinks Build(WebRequestContext request, WebPagination pagination, int window = 2, string parameter = DefaultParameter)
    {
        ArgumentNullException.ThrowIfNull(request);
        return BuildCore(RequestPath(request), request.QueryString, pagination, window, parameter);
    }

    /// <summary>Like <see cref="Build(WebRequestContext, WebPagination, int, string)"/> for an explicit decoded local path and query string.</summary>
    public static WebPagerLinks Build(string path, string? queryString, WebPagination pagination, int window = 2, string parameter = DefaultParameter) =>
        BuildCore(EncodePath(path), queryString, pagination, window, parameter);

    private static WebPagerLinks BuildCore(string encodedPath, string? queryString, WebPagination pagination, int window, string parameter)
    {
        ArgumentNullException.ThrowIfNull(pagination);
        ArgumentOutOfRangeException.ThrowIfNegative(window);
        string Url(int page) => Compose(encodedPath, queryString, page, parameter);

        var current = pagination.Page;
        var last = pagination.TotalPages;
        var from = Math.Max(1, current - window);
        var to = (int)Math.Min(last, (long)current + window);
        return new WebPagerLinks
        {
            Page = current,
            TotalPages = last,
            TotalItems = pagination.TotalItems,
            PreviousUrl = pagination.HasPrevious ? Url(current - 1) : null,
            NextUrl = pagination.HasNext ? Url(current + 1) : null,
            FirstUrl = current > 1 ? Url(1) : null,
            LastUrl = current < last ? Url(last) : null,
            Pages = [.. Enumerable.Range(from, to - from + 1).Select(n => new WebPagerLink(n, Url(n), n == current))]
        };
    }

    private static string Compose(string encodedPath, string? queryString, int page, string parameter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parameter);
        var query = new QueryBuilder();
        foreach (var (key, values) in QueryHelpers.ParseQuery(queryString))
        {
            if (string.Equals(key, parameter, StringComparison.OrdinalIgnoreCase))
                continue;
            foreach (var value in values)
                query.Add(key, value ?? string.Empty);
        }

        if (page > 1)
            query.Add(parameter, page.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return encodedPath + query.ToQueryString().Value;
    }

    private static string EncodePath(string path)
    {
        if (!WebUrl.IsLocal(path))
            throw new ArgumentException("The pager path must be a local path.", nameof(path));
        return new PathString(path).ToUriComponent();
    }

    /// <summary>The request's encoded path; leading slashes collapse to one so <c>//host</c> can never become a protocol-relative link.</summary>
    private static string RequestPath(WebRequestContext request)
    {
        var http = request.HttpContext.Request;
        var path = http.PathBase.Add(http.Path).ToUriComponent();
        return "/" + path.TrimStart('/', '\\');
    }
}

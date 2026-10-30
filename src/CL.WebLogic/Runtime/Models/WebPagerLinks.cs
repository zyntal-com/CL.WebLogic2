namespace CL.WebLogic.Runtime.Models;

/// <summary>
/// Ready-to-render pager links built by <see cref="Services.WebPageLinks.Build(WebRequestContext, WebPagination, int, string)"/>.
/// Every URL is local and already query-encoded; put them in <c>href</c> as they are. URLs for pages
/// that do not exist are null, so <c>{if:model:pager.nextUrl}</c> hides the link.
/// </summary>
public sealed record WebPagerLinks
{
    /// <summary>The current page.</summary>
    public required int Page { get; init; }

    /// <summary>Number of pages.</summary>
    public required int TotalPages { get; init; }

    /// <summary>Total number of items.</summary>
    public required long TotalItems { get; init; }

    /// <summary>The previous page, or null on the first page.</summary>
    public string? PreviousUrl { get; init; }

    /// <summary>The next page, or null on the last page.</summary>
    public string? NextUrl { get; init; }

    /// <summary>The first page, or null when it is the current one.</summary>
    public string? FirstUrl { get; init; }

    /// <summary>The last page, or null when it is the current one.</summary>
    public string? LastUrl { get; init; }

    /// <summary>Numbered links around the current page (the window), in order.</summary>
    public IReadOnlyList<WebPagerLink> Pages { get; init; } = [];
}

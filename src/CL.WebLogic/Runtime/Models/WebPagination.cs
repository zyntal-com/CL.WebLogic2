using System.Globalization;

namespace CL.WebLogic.Runtime.Models;

/// <summary>
/// Page arithmetic for a list: the current page, its size, the total and what follows from them.
/// Values are clamped on creation, so a request for page 9999 or a page size of a million cannot
/// produce a huge offset or an empty "page 9999 of 3". Build links with
/// <see cref="Services.WebPageLinks"/>.
/// </summary>
public sealed class WebPagination
{
    /// <summary>Default upper bound for <see cref="PageSize"/>.</summary>
    public const int DefaultMaxPageSize = 100;

    private WebPagination(int page, int pageSize, long totalItems, int totalPages)
    {
        Page = page;
        PageSize = pageSize;
        TotalItems = totalItems;
        TotalPages = totalPages;
    }

    /// <summary>The current page, 1-based, between 1 and <see cref="TotalPages"/>.</summary>
    public int Page { get; }

    /// <summary>Items per page, between 1 and the max page size.</summary>
    public int PageSize { get; }

    /// <summary>Total number of items (never negative).</summary>
    public long TotalItems { get; }

    /// <summary>Number of pages; at least 1, so an empty list is "page 1 of 1".</summary>
    public int TotalPages { get; }

    /// <summary>True when there is a page before this one.</summary>
    public bool HasPrevious => Page > 1;

    /// <summary>True when there is a page after this one.</summary>
    public bool HasNext => Page < TotalPages;

    /// <summary>Items to skip for this page (SQL <c>OFFSET</c>).</summary>
    public long Skip => (long)(Page - 1) * PageSize;

    /// <summary>1-based number of the first item on this page, or 0 when the list is empty.</summary>
    public long FirstItem => TotalItems == 0 ? 0 : Skip + 1;

    /// <summary>1-based number of the last item on this page, or 0 when the list is empty.</summary>
    public long LastItem => Math.Min(Skip + PageSize, TotalItems);

    /// <summary>
    /// Creates the pagination. <paramref name="pageSize"/> is clamped to 1..<paramref name="maxPageSize"/>,
    /// <paramref name="totalItems"/> to at least 0 and <paramref name="page"/> to 1..<see cref="TotalPages"/>.
    /// </summary>
    public static WebPagination Create(int page, int pageSize, long totalItems, int maxPageSize = DefaultMaxPageSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxPageSize, 1);
        var size = Math.Clamp(pageSize, 1, maxPageSize);
        var total = Math.Max(0, totalItems);
        var pages = (int)Math.Clamp((total + size - 1) / size, 1, int.MaxValue);
        return new WebPagination(Math.Clamp(page, 1, pages), size, total, pages);
    }

    /// <summary>
    /// Takes this page's slice of an in-memory sequence. For databases, count first, then query with
    /// <see cref="Skip"/> and <see cref="PageSize"/> instead.
    /// </summary>
    public WebPagedResult<T> Apply<T>(IEnumerable<T> all)
    {
        ArgumentNullException.ThrowIfNull(all);
        return new WebPagedResult<T>([.. all.Skip((int)Math.Min(Skip, int.MaxValue)).Take(PageSize)], this);
    }

    /// <summary>
    /// Parses a page number from the query string: digits only, 1 or more. Anything else (missing,
    /// negative, signs, overflow) is page 1. Use with <see cref="Create"/>, which clamps the upper end.
    /// </summary>
    public static int ParsePage(string? value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var page) && page >= 1 ? page : 1;

    /// <summary>Like <see cref="ParsePage(string?)"/>, reading the <paramref name="parameter"/> query value of <paramref name="request"/>.</summary>
    public static int ParsePage(WebRequestContext request, string parameter = "page")
    {
        ArgumentNullException.ThrowIfNull(request);
        return ParsePage(request.GetQuery(parameter));
    }
}

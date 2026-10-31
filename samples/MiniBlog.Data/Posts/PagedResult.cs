namespace MiniBlog.Data.Posts;

/// <summary>One page of results with the total for the pager.</summary>
/// <typeparam name="T">The item type.</typeparam>
public sealed class PagedResult<T>
{
    /// <summary>The items on this page.</summary>
    public List<T> Items { get; init; } = [];

    /// <summary>The page number (1-based).</summary>
    public int PageNumber { get; init; }

    /// <summary>The page size.</summary>
    public int PageSize { get; init; }

    /// <summary>Items on every page together.</summary>
    public long TotalItems { get; init; }
}

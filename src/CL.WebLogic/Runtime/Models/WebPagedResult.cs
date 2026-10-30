namespace CL.WebLogic.Runtime.Models;

/// <summary>One page of items with its <see cref="WebPagination"/>. Templates see <c>items</c> and <c>pagination</c>.</summary>
/// <typeparam name="T">The item type.</typeparam>
/// <param name="Items">The items on this page (at most <see cref="WebPagination.PageSize"/>).</param>
/// <param name="Pagination">Page arithmetic for the whole list.</param>
public sealed record WebPagedResult<T>(IReadOnlyList<T> Items, WebPagination Pagination);

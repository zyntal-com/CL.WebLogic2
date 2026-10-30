namespace CL.WebLogic.Runtime.Models;

/// <summary>One numbered pager link.</summary>
/// <param name="Number">The page number.</param>
/// <param name="Url">Local, encoded URL of the page.</param>
/// <param name="IsCurrent">True for the current page (render it as text with <c>aria-current="page"</c>).</param>
public sealed record WebPagerLink(int Number, string Url, bool IsCurrent);

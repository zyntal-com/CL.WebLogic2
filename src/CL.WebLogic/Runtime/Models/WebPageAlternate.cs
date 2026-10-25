namespace CL.WebLogic.Runtime.Models;

/// <summary>An alternate-language version of a page (<c>&lt;link rel="alternate" hreflang&gt;</c>).</summary>
public sealed class WebPageAlternate
{
    /// <summary>Language code, e.g. <c>da</c> or <c>x-default</c>.</summary>
    public required string Language { get; init; }

    /// <summary>Absolute URL of the alternate.</summary>
    public required string Url { get; init; }
}

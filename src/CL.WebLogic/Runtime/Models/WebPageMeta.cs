namespace CL.WebLogic.Runtime.Models;

/// <summary>Head metadata for a page, written by <c>{renderhead}</c>.</summary>
public sealed class WebPageMeta
{
    /// <summary>Page title.</summary>
    public string? Title { get; init; }

    /// <summary>Meta description.</summary>
    public string? Description { get; init; }

    /// <summary>Absolute canonical URL.</summary>
    public string? CanonicalUrl { get; init; }

    /// <summary>Robots directive, e.g. <c>noindex, nofollow</c>.</summary>
    public string? Robots { get; init; }

    /// <summary>Meta keywords.</summary>
    public IReadOnlyList<string> Keywords { get; init; } = [];

    /// <summary>Content language, e.g. <c>en</c>.</summary>
    public string? Language { get; init; }

    /// <summary>Alternate-language versions of the page.</summary>
    public IReadOnlyList<WebPageAlternate> Alternates { get; init; } = [];

    /// <summary>Open Graph metadata.</summary>
    public WebOpenGraphMeta? OpenGraph { get; init; }

    /// <summary>Twitter card metadata.</summary>
    public WebTwitterMeta? Twitter { get; init; }

    /// <summary>
    /// Structured data objects (schema.org) written as <c>application/ld+json</c> scripts.
    /// Serialized safely — values cannot break out of the script element.
    /// </summary>
    public IReadOnlyList<object> JsonLd { get; init; } = [];
}

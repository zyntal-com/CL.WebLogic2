namespace CL.WebLogic.Runtime.Models;

/// <summary>Open Graph tags. Unset values fall back to the page title, description and canonical URL.</summary>
public sealed class WebOpenGraphMeta
{
    /// <summary>og:title.</summary>
    public string? Title { get; init; }

    /// <summary>og:description.</summary>
    public string? Description { get; init; }

    /// <summary>og:type, e.g. <c>website</c> or <c>article</c>.</summary>
    public string? Type { get; init; }

    /// <summary>og:url.</summary>
    public string? Url { get; init; }

    /// <summary>og:image (absolute URL).</summary>
    public string? Image { get; init; }

    /// <summary>og:image:alt.</summary>
    public string? ImageAlt { get; init; }

    /// <summary>og:site_name.</summary>
    public string? SiteName { get; init; }

    /// <summary>og:locale.</summary>
    public string? Locale { get; init; }

    /// <summary>article:published_time (for <c>article</c>).</summary>
    public DateTimeOffset? PublishedTime { get; init; }

    /// <summary>article:modified_time (for <c>article</c>).</summary>
    public DateTimeOffset? ModifiedTime { get; init; }
}

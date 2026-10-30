namespace CL.WebLogic.Seo.Models;

/// <summary>The sitemap protocol's <c>changefreq</c> values.</summary>
public enum WebSitemapChangeFrequency
{
    /// <summary>Changes on every access.</summary>
    Always,

    /// <summary>Hourly.</summary>
    Hourly,

    /// <summary>Daily.</summary>
    Daily,

    /// <summary>Weekly.</summary>
    Weekly,

    /// <summary>Monthly.</summary>
    Monthly,

    /// <summary>Yearly.</summary>
    Yearly,

    /// <summary>Archived; never changes.</summary>
    Never
}

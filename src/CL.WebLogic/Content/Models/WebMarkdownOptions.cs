using CL.WebLogic.Content.Services;

namespace CL.WebLogic.Content.Models;

/// <summary>Options for <see cref="WebMarkdownRenderer"/>.</summary>
public sealed class WebMarkdownOptions
{
    /// <summary>Scheme policy for link and image URLs; links failing it render as plain text.</summary>
    public UrlSchemePolicy UrlPolicy { get; init; } = UrlSchemePolicy.Default;

    /// <summary><c>rel</c> written on links, or null for none.</summary>
    public string? LinkRel { get; init; }

    /// <summary>Whether <c>![alt](src)</c> renders images (otherwise the alt text is written).</summary>
    public bool AllowImages { get; init; } = true;

    /// <summary>Whether bare <c>https://…</c> and <c>www.…</c> text becomes links (GFM autolink extension).</summary>
    public bool AutoLinkBareUrls { get; init; } = true;

    /// <summary>Whether headings get <c>id</c> attributes (<see cref="HeadingIdPrefix"/> + slug).</summary>
    public bool GenerateHeadingIds { get; init; }

    /// <summary>Prefix for generated heading ids; a prefix avoids clobbering page globals.</summary>
    public string HeadingIdPrefix { get; init; } = "h-";

    /// <summary>Inputs longer than this are written as encoded plain text instead of being parsed.</summary>
    public int MaxInputLength { get; init; } = 512 * 1024;

    /// <summary>Defaults for trusted authors.</summary>
    public static WebMarkdownOptions Default { get; } = new();

    /// <summary>Defaults for user-generated content: links get <c>rel="nofollow ugc noopener noreferrer"</c>.</summary>
    public static WebMarkdownOptions UserContent { get; } = new() { LinkRel = "nofollow ugc noopener noreferrer" };
}

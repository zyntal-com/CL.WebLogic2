using CL.WebLogic.Content.Services;

namespace CL.WebLogic.Content.Models;

/// <summary>
/// Allowlists for <see cref="WebHtmlSanitizer"/>. The defaults suit user-written rich text
/// (comments, posts): formatting, lists, tables, links and images — no scripts, styles, forms,
/// frames, ids or event handlers.
/// </summary>
public sealed class WebHtmlSanitizerOptions
{
    /// <summary>Elements kept (others are unwrapped: their text survives).</summary>
    public ISet<string> AllowedElements { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "p", "br", "hr", "h1", "h2", "h3", "h4", "h5", "h6", "blockquote", "pre", "code",
        "em", "strong", "b", "i", "u", "s", "del", "ins", "mark", "sub", "sup", "small", "span", "div",
        "ul", "ol", "li", "dl", "dt", "dd", "a", "img", "figure", "figcaption", "abbr", "kbd", "q", "cite",
        "table", "thead", "tbody", "tfoot", "tr", "th", "td", "caption", "details", "summary"
    };

    /// <summary>Elements removed together with everything inside them.</summary>
    public ISet<string> DroppedElements { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "script", "style", "template", "iframe", "frame", "frameset", "object", "embed", "applet", "noscript",
        "svg", "math", "textarea", "select", "option", "title", "head", "xmp", "noembed", "noframes", "plaintext"
    };

    /// <summary>Attributes allowed on every kept element.</summary>
    public ISet<string> GlobalAttributes { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "title", "lang", "dir", "class"
    };

    /// <summary>Additional attributes per element.</summary>
    public IDictionary<string, ISet<string>> ElementAttributes { get; init; } = new Dictionary<string, ISet<string>>(StringComparer.OrdinalIgnoreCase)
    {
        ["a"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "href", "target" },
        ["img"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "src", "alt", "width", "height", "loading" },
        ["td"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "colspan", "rowspan" },
        ["th"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "colspan", "rowspan", "scope" },
        ["ol"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "start", "reversed", "type" },
        ["li"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "value" },
        ["blockquote"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "cite" },
        ["q"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "cite" },
        ["details"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "open" }
    };

    /// <summary>Attributes whose values are URLs and must pass <see cref="UrlPolicy"/>.</summary>
    public ISet<string> UrlAttributes { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "href", "src", "cite" };

    /// <summary>Scheme policy for URL attributes.</summary>
    public UrlSchemePolicy UrlPolicy { get; init; } = UrlSchemePolicy.Default;

    /// <summary><c>rel</c> written on every kept link (e.g. <c>nofollow ugc noopener</c>); null writes none.</summary>
    public string? LinkRel { get; init; } = "nofollow ugc noopener noreferrer";

    /// <summary>Maximum element nesting kept; deeper elements are unwrapped.</summary>
    public int MaxDepth { get; init; } = 64;

    /// <summary>Default options.</summary>
    public static WebHtmlSanitizerOptions Default { get; } = new();
}

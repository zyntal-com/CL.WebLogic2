namespace CL.WebLogic.Content.Services;

/// <summary>
/// Decides whether a URL may be written into an <c>href</c> / <c>src</c>-like attribute.
/// Relative URLs, fragments and allowlisted schemes pass; <c>javascript:</c>, <c>vbscript:</c>,
/// <c>data:</c> (except images when enabled) and unknown schemes are refused. Leading
/// whitespace, control characters and entity-like obfuscation are taken into account.
/// </summary>
public sealed class UrlSchemePolicy
{
    private readonly HashSet<string> _schemes;

    /// <summary>Creates a policy allowing <paramref name="schemes"/> (lower case, without <c>:</c>).</summary>
    public UrlSchemePolicy(IEnumerable<string> schemes, bool allowDataImages = false)
    {
        _schemes = new HashSet<string>(schemes ?? [], StringComparer.OrdinalIgnoreCase);
        AllowDataImages = allowDataImages;
    }

    /// <summary>http, https, mailto and tel.</summary>
    public static UrlSchemePolicy Default { get; } = new(["http", "https", "mailto", "tel"]);

    /// <summary>Like <see cref="Default"/> but also allows <c>data:image/(png|jpeg|gif|webp)</c> (for sanitized rich text).</summary>
    public static UrlSchemePolicy WithDataImages { get; } = new(["http", "https", "mailto", "tel"], allowDataImages: true);

    /// <summary>Whether base64 raster <c>data:</c> images are allowed.</summary>
    public bool AllowDataImages { get; }

    /// <summary>True when <paramref name="url"/> is safe to place in a URL attribute.</summary>
    public bool IsAllowed(string? url)
    {
        if (url is null)
            return false;

        // Browsers ignore leading/trailing whitespace and strip tabs/newlines inside the scheme.
        var compact = new string(url.Trim().Where(c => !char.IsControl(c) && c != ' ').ToArray());
        if (compact.Length == 0)
            return true;

        // Entity or percent tricks in the scheme part are never legitimate.
        var colon = compact.IndexOf(':');
        var firstDelimiter = compact.IndexOfAny(['/', '?', '#']);
        if (colon < 0 || (firstDelimiter >= 0 && firstDelimiter < colon))
            return !compact.StartsWith("//", StringComparison.Ordinal) || IsAllowedScheme("https");

        var scheme = compact.Substring(0, colon);
        if (scheme.Contains('&') || scheme.Contains('%') || scheme.Contains('\\'))
            return false;

        if (string.Equals(scheme, "data", StringComparison.OrdinalIgnoreCase))
            return AllowDataImages && IsRasterDataImage(compact);

        return IsAllowedScheme(scheme);
    }

    private bool IsAllowedScheme(string scheme) => _schemes.Contains(scheme);

    private static bool IsRasterDataImage(string url) =>
        url.StartsWith("data:image/png;base64,", StringComparison.OrdinalIgnoreCase)
        || url.StartsWith("data:image/jpeg;base64,", StringComparison.OrdinalIgnoreCase)
        || url.StartsWith("data:image/gif;base64,", StringComparison.OrdinalIgnoreCase)
        || url.StartsWith("data:image/webp;base64,", StringComparison.OrdinalIgnoreCase);
}

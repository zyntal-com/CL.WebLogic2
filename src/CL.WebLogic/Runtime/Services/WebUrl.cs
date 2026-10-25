namespace CL.WebLogic.Runtime.Services;

/// <summary>URL safety helpers shared by redirects and return-URL handling.</summary>
public static class WebUrl
{
    /// <summary>
    /// True when <paramref name="url"/> is a local, rooted path that a browser cannot interpret
    /// as another origin: it starts with a single <c>/</c>, is not protocol-relative
    /// (<c>//host</c>), has no backslash tricks (<c>/\host</c>) and no control characters.
    /// </summary>
    public static bool IsLocal(string? url)
    {
        if (string.IsNullOrEmpty(url) || url[0] != '/')
            return false;

        if (url.Length > 1 && (url[1] == '/' || url[1] == '\\'))
            return false;

        foreach (var c in url)
        {
            if (char.IsControl(c) || c == '\\')
                return false;
        }

        return true;
    }

    /// <summary>
    /// <paramref name="url"/> when it is a safe local return URL (see <see cref="IsLocal"/>), else
    /// <paramref name="fallback"/>. Use for every <c>returnUrl</c> taken from a request.
    /// </summary>
    public static string SafeReturnUrl(string? url, string fallback = "/") =>
        IsLocal(url) && url!.Length <= 2048 ? url : fallback;
}

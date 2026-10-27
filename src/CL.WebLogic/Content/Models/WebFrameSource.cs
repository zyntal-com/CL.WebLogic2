namespace CL.WebLogic.Content.Models;

/// <summary>An embed origin the sanitizer keeps iframes for: an https host and a path prefix.</summary>
/// <param name="Host">Exact host name, e.g. <c>www.youtube-nocookie.com</c> (no wildcards).</param>
/// <param name="PathPrefix">Path the URL must start with, e.g. <c>/embed/</c>; <c>/</c> allows any path.</param>
public sealed record WebFrameSource(string Host, string PathPrefix = "/")
{
    /// <summary>True when <paramref name="url"/> is an https URL on <see cref="Host"/> under <see cref="PathPrefix"/>.</summary>
    public bool Matches(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            return false;
        return uri.Scheme == Uri.UriSchemeHttps
            && uri.IsDefaultPort
            && string.IsNullOrEmpty(uri.UserInfo)
            && string.Equals(uri.Host, Host, StringComparison.OrdinalIgnoreCase)
            && uri.AbsolutePath.StartsWith(string.IsNullOrEmpty(PathPrefix) ? "/" : PathPrefix, StringComparison.Ordinal);
    }
}

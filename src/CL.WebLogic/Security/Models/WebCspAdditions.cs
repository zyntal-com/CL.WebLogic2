namespace CL.WebLogic.Security.Models;

/// <summary>
/// Extra Content-Security-Policy sources for one route or group (e.g. a video embed on a single
/// page), added to the site-wide policy instead of widening it. Sources are validated when added:
/// hosts, schemes (<c>https:</c>, <c>data:</c>, <c>blob:</c>) and <c>'self'</c> are accepted; other
/// keywords such as <c>'unsafe-inline'</c> are refused.
/// </summary>
public sealed class WebCspAdditions
{
    private readonly Dictionary<string, List<string>> _sources = new(StringComparer.Ordinal);

    /// <summary>Sources per directive (e.g. <c>frame-src</c>).</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Sources =>
        _sources.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value.ToArray(), StringComparer.Ordinal);

    /// <summary>True when nothing was added.</summary>
    public bool IsEmpty => _sources.Count == 0;

    /// <summary>Adds a <c>script-src</c> source (with <c>'strict-dynamic'</c>, browsers mostly ignore host sources).</summary>
    public WebCspAdditions AddScriptSource(string source) => Add("script-src", source);

    /// <summary>Adds a <c>style-src</c> source.</summary>
    public WebCspAdditions AddStyleSource(string source) => Add("style-src", source);

    /// <summary>Adds an <c>img-src</c> source.</summary>
    public WebCspAdditions AddImageSource(string source) => Add("img-src", source);

    /// <summary>Adds a <c>connect-src</c> source.</summary>
    public WebCspAdditions AddConnectSource(string source) => Add("connect-src", source);

    /// <summary>Adds a <c>font-src</c> source.</summary>
    public WebCspAdditions AddFontSource(string source) => Add("font-src", source);

    /// <summary>Adds a <c>frame-src</c> source (embedded players, maps).</summary>
    public WebCspAdditions AddFrameSource(string source) => Add("frame-src", source);

    /// <summary>Adds a <c>media-src</c> source (audio and video files).</summary>
    public WebCspAdditions AddMediaSource(string source) => Add("media-src", source);

    /// <summary>Adds a <c>worker-src</c> source.</summary>
    public WebCspAdditions AddWorkerSource(string source) => Add("worker-src", source);

    /// <summary>Adds a <c>manifest-src</c> source.</summary>
    public WebCspAdditions AddManifestSource(string source) => Add("manifest-src", source);

    /// <summary>Adds a <c>form-action</c> target (forms posting to another site, e.g. a payment provider).</summary>
    public WebCspAdditions AddFormAction(string source) => Add("form-action", source);

    /// <summary>True when <paramref name="source"/> is safe to place in a policy (see the class remarks).</summary>
    public static bool IsValidSource(string? source)
    {
        if (string.IsNullOrEmpty(source) || source.Length > 256)
            return false;
        if (source.StartsWith('\''))
            return source is "'self'";
        return source.All(c => c > ' ' && c < 127 && c is not (';' or ',' or '\'' or '"'));
    }

    /// <summary>Adds everything from <paramref name="other"/> (group settings flowing into a route).</summary>
    public void MergeFrom(WebCspAdditions other)
    {
        ArgumentNullException.ThrowIfNull(other);
        foreach (var (directive, sources) in other._sources)
        {
            foreach (var source in sources)
                Add(directive, source);
        }
    }

    private WebCspAdditions Add(string directive, string source)
    {
        source = source?.Trim() ?? string.Empty;
        if (!IsValidSource(source))
            throw new ArgumentException($"'{source}' is not an allowed {directive} source (hosts, schemes such as https: or 'self').", nameof(source));
        if (!_sources.TryGetValue(directive, out var list))
            _sources[directive] = list = [];
        if (!list.Contains(source, StringComparer.Ordinal))
            list.Add(source);
        return this;
    }
}

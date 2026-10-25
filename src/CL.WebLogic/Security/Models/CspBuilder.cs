using CL.WebLogic.Configuration.Models;

namespace CL.WebLogic.Security.Models;

/// <summary>Builds the Content-Security-Policy template (with a <c>{nonce}</c> placeholder).</summary>
public static class CspBuilder
{
    /// <summary>The placeholder replaced with the per-request nonce.</summary>
    public const string NoncePlaceholder = "{nonce}";

    /// <summary>Builds the policy, or null when CSP is disabled.</summary>
    public static string? Build(ResponseHeadersConfig headers)
    {
        if (!headers.CspEnabled)
            return null;

        var directives = new List<string>
        {
            "default-src 'self'",
            Directive("script-src", ["'self'", "'nonce-" + NoncePlaceholder + "'", "'strict-dynamic'"], headers.ScriptSources),
            Directive("style-src", ["'self'", "'nonce-" + NoncePlaceholder + "'"], headers.StyleSources),
            Directive("img-src", ["'self'", "data:"], headers.ImageSources),
            Directive("font-src", ["'self'"], headers.FontSources),
            Directive("connect-src", ["'self'"], headers.ConnectSources),
            Directive("frame-src", headers.FrameSources.Count == 0 ? ["'none'"] : [], headers.FrameSources),
            "object-src 'none'",
            "base-uri 'self'",
            "form-action 'self'",
            Directive("frame-ancestors", headers.FrameAncestors.Count == 0 ? ["'none'"] : [], headers.FrameAncestors)
        };

        if (headers.AllowInlineStyleAttributes)
            directives.Add("style-src-attr 'unsafe-inline'");
        if (!string.IsNullOrWhiteSpace(headers.CspReportUri))
            directives.Add("report-uri " + Clean(headers.CspReportUri));

        return string.Join("; ", directives);
    }

    private static string Directive(string name, IEnumerable<string> defaults, IEnumerable<string> extra) =>
        name + " " + string.Join(' ', defaults.Concat(extra.Select(Clean)).Where(s => s.Length > 0).Distinct(StringComparer.Ordinal));

    /// <summary>Strips characters that could end a directive or header.</summary>
    private static string Clean(string source) =>
        new(source.Where(c => !char.IsControl(c) && c is not (';' or ',')).ToArray());
}

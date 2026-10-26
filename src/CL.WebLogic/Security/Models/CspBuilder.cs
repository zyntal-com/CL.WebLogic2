using CL.WebLogic.Configuration.Models;

namespace CL.WebLogic.Security.Models;

/// <summary>Builds the Content-Security-Policy template (with a <c>{nonce}</c> placeholder).</summary>
public static class CspBuilder
{
    /// <summary>The placeholder replaced with the per-request nonce.</summary>
    public const string NoncePlaceholder = "{nonce}";

    /// <summary>The <c>Reporting-Endpoints</c> name used by <c>report-to</c>.</summary>
    public const string ReportEndpointName = "csp-endpoint";

    /// <summary>
    /// Builds the policy, or null when CSP is disabled. <paramref name="additions"/> are a route's
    /// extra sources per directive (see <see cref="WebCspAdditions"/>).
    /// </summary>
    public static string? Build(ResponseHeadersConfig headers, IReadOnlyDictionary<string, IReadOnlyList<string>>? additions = null)
    {
        ArgumentNullException.ThrowIfNull(headers);
        if (!headers.CspEnabled)
            return null;

        IEnumerable<string> Extra(string directive, List<string> configured) =>
            additions is not null && additions.TryGetValue(directive, out var added) ? configured.Concat(added) : configured;

        var directives = new List<string>
        {
            "default-src 'self'",
            Directive("script-src", ["'self'", "'nonce-" + NoncePlaceholder + "'", "'strict-dynamic'"], Extra("script-src", headers.ScriptSources)),
            Directive("style-src", ["'self'", "'nonce-" + NoncePlaceholder + "'"], Extra("style-src", headers.StyleSources)),
            Directive("img-src", ["'self'", "data:"], Extra("img-src", headers.ImageSources)),
            Directive("font-src", ["'self'"], Extra("font-src", headers.FontSources)),
            Directive("connect-src", ["'self'"], Extra("connect-src", headers.ConnectSources))
        };
        // These fall back to default-src 'self'; they are only written when something is added.
        foreach (var (name, configured) in new[] { ("media-src", headers.MediaSources), ("worker-src", headers.WorkerSources), ("manifest-src", headers.ManifestSources) })
        {
            var extra = Extra(name, configured).ToList();
            if (extra.Count > 0)
                directives.Add(Directive(name, ["'self'"], extra));
        }
        directives.Add(NoneUnless("frame-src", Extra("frame-src", headers.FrameSources)));
        directives.Add("object-src 'none'");
        directives.Add("base-uri 'self'");
        directives.Add(Directive("form-action", ["'self'"], Extra("form-action", headers.FormActions)));
        directives.Add(NoneUnless("frame-ancestors", headers.FrameAncestors));

        if (headers.AllowInlineStyleAttributes)
            directives.Add("style-src-attr 'unsafe-inline'");
        if (!string.IsNullOrWhiteSpace(headers.CspReportUri))
            directives.Add("report-uri " + Clean(headers.CspReportUri));
        if (!string.IsNullOrWhiteSpace(headers.CspReportTo))
            directives.Add("report-to " + ReportEndpointName);

        return string.Join("; ", directives);
    }

    /// <summary>The <c>Reporting-Endpoints</c> header value, or null when <c>CspReportTo</c> is not set.</summary>
    public static string? ReportingEndpoints(ResponseHeadersConfig headers)
    {
        ArgumentNullException.ThrowIfNull(headers);
        return string.IsNullOrWhiteSpace(headers.CspReportTo) || !headers.CspEnabled
            ? null
            : ReportEndpointName + "=\"" + Clean(headers.CspReportTo).Replace("\"", "", StringComparison.Ordinal) + "\"";
    }

    private static string NoneUnless(string name, IEnumerable<string> sources)
    {
        var list = sources.ToList();
        return list.Count == 0 ? name + " 'none'" : Directive(name, [], list);
    }

    private static string Directive(string name, IEnumerable<string> defaults, IEnumerable<string> extra) =>
        name + " " + string.Join(' ', defaults.Concat(extra.Select(Clean)).Where(s => s.Length > 0).Distinct(StringComparer.Ordinal));

    /// <summary>Strips characters that could end a directive or header.</summary>
    private static string Clean(string source) =>
        new(source.Where(c => !char.IsControl(c) && c is not (';' or ',')).ToArray());
}

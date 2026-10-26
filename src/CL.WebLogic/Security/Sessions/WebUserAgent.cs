using System.Text.RegularExpressions;

namespace CL.WebLogic.Security.Sessions;

/// <summary>
/// Reads a <c>User-Agent</c> header as a device: browser and operating system family, without
/// versions, so a browser update is still the same device. Deliberately coarse — it names things for
/// people ("Firefox on macOS") and never decides anything security-relevant on its own.
/// </summary>
public static partial class WebUserAgent
{
    /// <summary>Browser family, e.g. <c>Chrome</c>, <c>Edge</c>, <c>Firefox</c>, <c>Safari</c> (null = not recognised).</summary>
    public static string? Browser(string? userAgent)
    {
        if (string.IsNullOrEmpty(userAgent))
            return null;
        // Order matters: Edge, Opera and Samsung also say Chrome and Safari; Chrome also says Safari.
        if (Has(userAgent, "Edg/") || Has(userAgent, "EdgiOS") || Has(userAgent, "EdgA/"))
            return "Edge";
        if (Has(userAgent, "OPR/") || Has(userAgent, "Opera"))
            return "Opera";
        if (Has(userAgent, "SamsungBrowser"))
            return "Samsung Internet";
        if (Has(userAgent, "Firefox/") || Has(userAgent, "FxiOS"))
            return "Firefox";
        if (Has(userAgent, "CriOS") || Has(userAgent, "Chrome/") || Has(userAgent, "Chromium/"))
            return "Chrome";
        if (Has(userAgent, "Safari/") && Has(userAgent, "Version/"))
            return "Safari";
        return null;
    }

    /// <summary>Operating system family, e.g. <c>Windows</c>, <c>macOS</c>, <c>iOS</c>, <c>Android</c> (null = not recognised).</summary>
    public static string? OperatingSystem(string? userAgent)
    {
        if (string.IsNullOrEmpty(userAgent))
            return null;
        if (Has(userAgent, "Windows"))
            return "Windows";
        if (Has(userAgent, "iPad"))
            return "iPadOS";
        if (Has(userAgent, "iPhone") || Has(userAgent, "iPod"))
            return "iOS";
        if (Has(userAgent, "Android"))
            return "Android";
        if (Has(userAgent, "CrOS"))
            return "ChromeOS";
        if (Has(userAgent, "Mac OS X") || Has(userAgent, "Macintosh"))
            return "macOS";
        if (Has(userAgent, "Linux"))
            return "Linux";
        return null;
    }

    /// <summary>A name for people: <c>Chrome on Windows</c>, <c>Firefox</c>, <c>Android</c>, or null when nothing is recognised.</summary>
    public static string? Describe(string? userAgent)
    {
        var browser = Browser(userAgent);
        var os = OperatingSystem(userAgent);
        return (browser, os) switch
        {
            (not null, not null) => browser + " on " + os,
            (not null, null) => browser,
            (null, not null) => os,
            _ => null
        };
    }

    /// <summary>
    /// A key that stays the same across version updates: browser and OS family when both are
    /// recognised, otherwise the header with every number removed. Two sessions with the same key
    /// count as the same device.
    /// </summary>
    public static string DeviceKey(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
            return string.Empty;
        var browser = Browser(userAgent);
        var os = OperatingSystem(userAgent);
        return browser is not null && os is not null
            ? browser + "|" + os
            : Numbers().Replace(userAgent, string.Empty).Trim().ToLowerInvariant();
    }

    private static bool Has(string userAgent, string token) => userAgent.Contains(token, StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"[0-9]+", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex Numbers();
}

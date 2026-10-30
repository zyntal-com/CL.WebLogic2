using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Diagnostics.Services;

/// <summary>
/// Turns request and security values into low-cardinality tag values: anything outside a small,
/// known shape collapses to <c>other</c>, so a tag can never carry an address, id or query string.
/// </summary>
internal static class WebMetricTags
{
    /// <summary>Used for values that do not match the expected shape.</summary>
    public const string Other = "other";

    private const int MaxLength = 64;

    private static readonly HashSet<string> KnownMethods =
        new(["pwd", "otp", "recovery", "link", "passkey", "custom"], StringComparer.Ordinal);

    /// <summary>The status class (<c>2xx</c>…), or <c>other</c>.</summary>
    public static string StatusClass(int statusCode) => statusCode switch
    {
        >= 100 and < 200 => "1xx",
        >= 200 and < 300 => "2xx",
        >= 300 and < 400 => "3xx",
        >= 400 and < 500 => "4xx",
        >= 500 and < 600 => "5xx",
        _ => Other
    };

    /// <summary>The standard method name, or <c>_OTHER</c> (OpenTelemetry convention).</summary>
    public static string Method(string method) =>
        HttpMethods.IsGet(method) ? HttpMethods.Get
        : HttpMethods.IsPost(method) ? HttpMethods.Post
        : HttpMethods.IsPut(method) ? HttpMethods.Put
        : HttpMethods.IsDelete(method) ? HttpMethods.Delete
        : HttpMethods.IsPatch(method) ? HttpMethods.Patch
        : HttpMethods.IsHead(method) ? HttpMethods.Head
        : HttpMethods.IsOptions(method) ? HttpMethods.Options
        : "_OTHER";

    /// <summary>
    /// The reason code without its variable part: <c>country-denied:DK</c> → <c>country-denied</c>,
    /// <c>forbidden:group:admin</c> → <c>forbidden</c>. Anything but a short <c>[a-z0-9._-]</c> code is <c>other</c>.
    /// </summary>
    public static string ReasonCode(string? reason)
    {
        if (string.IsNullOrEmpty(reason))
            return "rejected";
        var colon = reason.IndexOf(':', StringComparison.Ordinal);
        var code = colon < 0 ? reason : reason[..colon];
        return IsCode(code) ? code : Other;
    }

    /// <summary>A server-defined name (stage, template path) kept when it is a short code; <c>other</c> otherwise.</summary>
    public static string Name(string? name) =>
        !string.IsNullOrEmpty(name) && name.Length <= 128 && name.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-' or '/' or '{' or '}' or '*' or ':' or '?')
            ? name
            : Other;

    /// <summary>
    /// The sign-in methods (<c>pwd,otp</c>): known methods and <c>ext:&lt;provider&gt;</c> are kept,
    /// anything else (an app-chosen method name) becomes <c>other</c>.
    /// </summary>
    public static string Methods(IReadOnlyList<string>? methods)
    {
        if (methods is null || methods.Count == 0)
            return "none";
        if (methods.Count == 1)
            return SignInMethod(methods[0]);
        return string.Join(',', methods.Take(4).Select(SignInMethod));
    }

    private static string SignInMethod(string method) =>
        KnownMethods.Contains(method) ? method
        : method.StartsWith("ext:", StringComparison.Ordinal) && method.Length <= 36 && IsCode(method[4..]) ? method
        : Other;

    private static bool IsCode(string value) =>
        value.Length is > 0 and <= MaxLength
        && value.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c is '-' or '_' or '.');
}

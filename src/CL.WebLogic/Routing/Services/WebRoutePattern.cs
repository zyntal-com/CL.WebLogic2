namespace CL.WebLogic.Routing.Services;

/// <summary>Helpers for combining and normalizing route patterns and HTTP method lists.</summary>
internal static class WebRoutePattern
{
    /// <summary>Joins a group prefix and a relative pattern into a rooted pattern (<c>/admin</c> + <c>users/{id}</c>).</summary>
    public static string Combine(string prefix, string pattern)
    {
        var left = (prefix ?? string.Empty).Trim().Trim('/');
        var right = (pattern ?? string.Empty).Trim().Trim('/');

        if (left.Length == 0) return "/" + right;
        if (right.Length == 0) return "/" + left;
        return "/" + left + "/" + right;
    }

    /// <summary>Upper-cases and de-duplicates methods; falls back to GET when none are given.</summary>
    public static IReadOnlyList<string> NormalizeMethods(IEnumerable<string>? methods)
    {
        var result = (methods ?? [])
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Select(m => m.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return result.Count == 0 ? ["GET"] : result;
    }
}

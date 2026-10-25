using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;

namespace CL.WebLogic.Routing.Services;

/// <summary>
/// Path globs for include/exclude lists: <c>*</c> matches within one segment, <c>**</c> across
/// segments (<c>/blog/**</c>, <c>/docs/*/edit</c>). Case-insensitive.
/// </summary>
public static class WebPathPattern
{
    private static readonly ConcurrentDictionary<string, Regex> Cache = new(StringComparer.Ordinal);

    /// <summary>True when <paramref name="path"/> matches <paramref name="pattern"/> (false when the match times out).</summary>
    public static bool IsMatch(string pattern, string path) => Match(pattern, path, onTimeout: false);

    /// <summary>
    /// True when <paramref name="path"/> passes the include list (empty = everything) and no exclude.
    /// A pattern that times out fails closed: it does not include, and it does exclude.
    /// </summary>
    public static bool Passes(string path, IReadOnlyList<string> include, IReadOnlyList<string> exclude) =>
        (include.Count == 0 || include.Any(p => Match(p, path, onTimeout: false))) && !exclude.Any(p => Match(p, path, onTimeout: true));

    private static bool Match(string pattern, string path, bool onTimeout)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        if (Cache.Count > 4096)
            Cache.Clear();
        var regex = Cache.GetOrAdd(pattern, Compile);
        try
        {
            return regex.IsMatch(path ?? string.Empty);
        }
        catch (RegexMatchTimeoutException)
        {
            return onTimeout;
        }
    }

    private static Regex Compile(string pattern)
    {
        var builder = new StringBuilder("^");
        for (var i = 0; i < pattern.Length; i++)
        {
            if (pattern[i] == '*')
            {
                var deep = i + 1 < pattern.Length && pattern[i + 1] == '*';
                builder.Append(deep ? ".*" : "[^/]*");
                if (deep)
                    i++;
            }
            else
            {
                builder.Append(Regex.Escape(pattern[i].ToString()));
            }
        }
        builder.Append('$');
        return new Regex(builder.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    }
}

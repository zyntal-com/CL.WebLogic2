namespace CL.WebLogic.Security.Models;

/// <summary>Matches Host header values against <c>example.com</c> / <c>*.example.com</c> patterns.</summary>
public sealed class HostMatcher
{
    private readonly string[] _exact;
    private readonly string[] _suffixes;

    /// <summary>Creates the matcher; an empty list matches every host.</summary>
    public HostMatcher(IEnumerable<string> patterns)
    {
        var list = patterns.Select(p => p.Trim().ToLowerInvariant()).Where(p => p.Length > 0).ToList();
        _exact = [.. list.Where(p => !p.StartsWith("*.", StringComparison.Ordinal))];
        _suffixes = [.. list.Where(p => p.StartsWith("*.", StringComparison.Ordinal)).Select(p => p.Substring(1))];
    }

    /// <summary>True when no hosts are configured.</summary>
    public bool AllowsAny => _exact.Length == 0 && _suffixes.Length == 0;

    /// <summary>True when <paramref name="host"/> (without port) is accepted.</summary>
    public bool IsAllowed(string? host)
    {
        if (AllowsAny)
            return true;
        if (string.IsNullOrEmpty(host))
            return false;
        var h = host.ToLowerInvariant();
        return _exact.Contains(h) || _suffixes.Any(s => h.EndsWith(s, StringComparison.Ordinal) && h.Length > s.Length);
    }
}

namespace CL.WebLogic.Security.Identity;

/// <summary>
/// Permission matching: an exact grant, <c>*</c> (everything) or a wildcard such as
/// <c>posts.*</c> (everything below <c>posts.</c>). Case-insensitive.
/// </summary>
public static class WebPermissionMatcher
{
    /// <summary>True when <paramref name="granted"/> covers <paramref name="required"/>.</summary>
    public static bool Grants(IEnumerable<string> granted, string required)
    {
        if (string.IsNullOrWhiteSpace(required))
            return false;

        foreach (var grant in granted)
        {
            if (grant == "*" || string.Equals(grant, required, StringComparison.OrdinalIgnoreCase))
                return true;
            if (grant.EndsWith(".*", StringComparison.Ordinal)
                && required.StartsWith(grant.Substring(0, grant.Length - 1), StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>True when <paramref name="granted"/> covers every one of <paramref name="required"/>.</summary>
    public static bool GrantsAll(IEnumerable<string> granted, IEnumerable<string> required)
    {
        var grants = granted as IReadOnlyCollection<string> ?? granted.ToList();
        return required.All(r => Grants(grants, r));
    }
}

namespace CL.WebLogic.Auth.Accounts;

/// <summary>Basic checks for user names and e-mail addresses.</summary>
public static class WebAccountValidation
{
    /// <summary>1–64 characters, no control characters, no surrounding spaces.</summary>
    public static bool IsValidUserName(string? userName) =>
        userName is { Length: > 0 and <= 64 } && userName.Trim().Length == userName.Length && !userName.Any(char.IsControl);

    /// <summary>A plausible address: one <c>@</c>, a dotted domain, at most 254 characters, no spaces or markup.</summary>
    public static bool IsValidEmail(string? email)
    {
        if (email is null || email.Length is < 3 or > 254 || email.Any(c => char.IsWhiteSpace(c) || char.IsControl(c) || c is '<' or '>' or '"' or ',' or ';'))
            return false;
        var at = email.IndexOf('@', StringComparison.Ordinal);
        if (at <= 0 || at != email.LastIndexOf('@') || at == email.Length - 1)
            return false;
        var domain = email[(at + 1)..];
        return domain.Contains('.', StringComparison.Ordinal) && !domain.StartsWith('.') && !domain.EndsWith('.');
    }
}

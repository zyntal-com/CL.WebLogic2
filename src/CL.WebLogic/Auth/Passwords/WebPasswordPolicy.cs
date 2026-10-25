using CL.WebLogic.Auth.Users;
using CL.WebLogic.Configuration.Services;
using CodeLogic.Core.Results;

namespace CL.WebLogic.Auth.Passwords;

/// <summary>
/// Default policy, following NIST SP 800-63B: a minimum and maximum length, no composition rules,
/// but no very common passwords, no single repeated character and no passwords containing the
/// user name or e-mail name.
/// </summary>
public sealed class WebPasswordPolicy : IWebPasswordPolicy
{
    private static readonly HashSet<string> Common = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "password1", "password123", "passw0rd", "123456789", "1234567890", "12345678910", "qwertyuiop",
        "qwerty123", "1q2w3e4r5t", "iloveyou12", "letmein123", "welcome123", "administrator", "abc123456", "football12",
        "monkey1234", "dragon1234", "sunshine12", "princess12", "trustno1234", "changeme123", "0123456789", "987654321"
    };

    private readonly WebLogicConfigHolder _config;

    /// <summary>Creates the policy.</summary>
    public WebPasswordPolicy(WebLogicConfigHolder config) => _config = config ?? throw new ArgumentNullException(nameof(config));

    /// <inheritdoc/>
    public Result Validate(string password, WebUser? user)
    {
        var settings = _config.Current.Auth.Passwords;
        password ??= string.Empty;
        if (password.Length < settings.MinimumLength)
            return Fail($"Use at least {settings.MinimumLength} characters.");
        if (password.Length > settings.MaximumLength)
            return Fail($"Use at most {settings.MaximumLength} characters.");
        if (password.Distinct().Count() == 1 || Common.Contains(password))
            return Fail("This password is too common.");
        if (user is not null && (Contains(password, user.UserName) || Contains(password, user.Email?.Split('@')[0])))
            return Fail("The password must not contain your user name or e-mail.");
        return Result.Success();
    }

    private static bool Contains(string password, string? part) =>
        part is { Length: >= 3 } && password.Contains(part, StringComparison.OrdinalIgnoreCase);

    private static Result Fail(string message) => Result.Failure(Error.Validation("weblogic.auth.password_policy", message));
}

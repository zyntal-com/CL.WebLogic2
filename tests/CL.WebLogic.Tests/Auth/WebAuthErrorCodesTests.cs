using CL.WebLogic.Auth.Accounts;

namespace CL.WebLogic.Tests.Auth;

public sealed class WebAuthErrorCodesTests
{
    // The codes are public contract: apps compare against them, so a rename is a breaking change.
    [Theory]
    [InlineData(WebAuthErrorCodes.WrongPassword, "weblogic.auth.wrong_password")]
    [InlineData(WebAuthErrorCodes.PasswordPolicy, "weblogic.auth.password_policy")]
    [InlineData(WebAuthErrorCodes.DuplicateUser, "weblogic.auth.duplicate_user")]
    [InlineData(WebAuthErrorCodes.InvalidEmail, "weblogic.auth.invalid_email")]
    [InlineData(WebAuthErrorCodes.TokenInvalid, "weblogic.auth.token_invalid")]
    [InlineData(WebAuthErrorCodes.NotSignedIn, "weblogic.auth.not_signed_in")]
    [InlineData(WebAuthErrorCodes.UserNotFound, "weblogic.auth.user_not_found")]
    public void Codes_AreStable(string constant, string expected) => Assert.Equal(expected, constant);

    [Fact]
    public void Codes_AreUnique()
    {
        var codes = typeof(WebAuthErrorCodes).GetFields()
            .Where(f => f.IsLiteral)
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        Assert.NotEmpty(codes);
        Assert.Equal(codes.Count, codes.Distinct(StringComparer.Ordinal).Count());
        Assert.All(codes, c => Assert.StartsWith("weblogic.auth.", c, StringComparison.Ordinal));
    }
}

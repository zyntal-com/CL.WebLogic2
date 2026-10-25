using CL.WebLogic.Auth.Accounts;
using CL.WebLogic.Auth.ApiKeys;
using CL.WebLogic.Auth.Passwords;
using CL.WebLogic.Auth.Throttling;
using CL.WebLogic.Auth.Tokens;
using CL.WebLogic.Auth.TwoFactor;
using CL.WebLogic.Auth.Users;
using CL.WebLogic.Configuration.Models;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Testing.Services;
using Microsoft.AspNetCore.DataProtection;

namespace CL.WebLogic.Tests.Auth;

public sealed class AuthPrimitivesTests
{
    private static WebLogicConfigHolder Config(Action<WebLogicConfig>? configure = null)
    {
        var config = new WebLogicConfig();
        config.Auth.Passwords.Iterations = 100_000;
        configure?.Invoke(config);
        return new WebLogicConfigHolder(config);
    }

    [Fact]
    public void Pbkdf2_RoundTrips_WithTheDocumentedFormat()
    {
        var hasher = new Pbkdf2PasswordHasher(Config());
        var hash = hasher.Hash("s3cret pass");

        Assert.Matches(@"^\$wl\$pbkdf2-sha256\$v=1\$i=100000\$[A-Za-z0-9_-]{22}\$[A-Za-z0-9_-]{43}$", hash);
        Assert.NotEqual(hash, hasher.Hash("s3cret pass")); // salted
        Assert.Equal(WebPasswordVerification.Success, hasher.Verify(hash, "s3cret pass"));
        Assert.Equal(WebPasswordVerification.Failed, hasher.Verify(hash, "s3cret pasS"));
    }

    [Fact]
    public void Pbkdf2_AsksForRehash_WhenParametersChange()
    {
        var old = new Pbkdf2PasswordHasher(Config()).Hash("pw");

        Assert.Equal(WebPasswordVerification.SuccessRehashNeeded, new Pbkdf2PasswordHasher(Config(c => c.Auth.Passwords.Iterations = 200_000)).Verify(old, "pw"));
        Assert.Equal(WebPasswordVerification.SuccessRehashNeeded, new Pbkdf2PasswordHasher(Config(c => c.Auth.Passwords.Pepper = "pep")).Verify(old, "pw"));
    }

    [Fact]
    public void Pbkdf2_Pepper_IsRequiredToVerify()
    {
        var peppered = new Pbkdf2PasswordHasher(Config(c => c.Auth.Passwords.Pepper = "pep"));
        var hash = peppered.Hash("pw");

        Assert.Contains("$pbkdf2-sha256-p$", hash);
        Assert.Equal(WebPasswordVerification.Success, peppered.Verify(hash, "pw"));
        Assert.Equal(WebPasswordVerification.Failed, new Pbkdf2PasswordHasher(Config(c => c.Auth.Passwords.Pepper = "other")).Verify(hash, "pw"));
        Assert.Equal(WebPasswordVerification.Failed, new Pbkdf2PasswordHasher(Config()).Verify(hash, "pw"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("plain")]
    [InlineData("$wl$pbkdf2-sha256$v=1$i=abc$x$y")]
    [InlineData("$wl$pbkdf2-sha256$v=2$i=1000$AAAA$AAAA")]
    [InlineData("$wl$pbkdf2-sha256$v=1$i=1000$!!!$AAAA")]
    [InlineData("$wl$unknown$x$y")]
    public void Pbkdf2_RejectsGarbage(string hash) =>
        Assert.Equal(WebPasswordVerification.Failed, new Pbkdf2PasswordHasher(Config()).Verify(hash, "pw"));

    [Fact]
    public void LegacyAlgorithms_VerifyAndRequestUpgrade()
    {
        var hasher = new Pbkdf2PasswordHasher(Config(), [new PlainLegacy()]);

        Assert.Equal(WebPasswordVerification.SuccessRehashNeeded, hasher.Verify("$wl$legacy$x$pw", "pw"));
        Assert.Equal(WebPasswordVerification.Failed, hasher.Verify("$wl$legacy$x$pw", "nope"));
    }

    [Theory]
    [InlineData("short", false)]
    [InlineData("password123", false)]
    [InlineData("aaaaaaaaaaaa", false)]
    [InlineData("my alice password", false)]
    [InlineData("correct horse battery", true)]
    public void PasswordPolicy(string password, bool ok)
    {
        var user = new WebUser { Id = "1", UserName = "alice", Email = "alice@example.test" };
        Assert.Equal(ok, new WebPasswordPolicy(Config()).Validate(password, user).IsSuccess);
    }

    [Fact]
    public void RecoveryCodes_AreUnambiguous_AndHashPerUser()
    {
        var codes = WebRecoveryCodes.Generate(10);

        Assert.Equal(10, codes.Distinct().Count());
        Assert.All(codes, c => Assert.Matches("^[A-HJ-NP-Z2-9]{5}-[A-HJ-NP-Z2-9]{5}$", c));
        Assert.Equal(WebRecoveryCodes.Hash("u1", codes[0]), WebRecoveryCodes.Hash("u1", " " + codes[0].ToLowerInvariant().Replace("-", "", StringComparison.Ordinal)));
        Assert.NotEqual(WebRecoveryCodes.Hash("u1", codes[0]), WebRecoveryCodes.Hash("u2", codes[0]));
    }

    [Fact]
    public void ApiKeyFormat_ParsesOnlyWellFormedKeys()
    {
        var (key, prefix) = WebApiKeyFormat.Generate();

        Assert.True(WebApiKeyFormat.TryParse(key, out var parsed));
        Assert.Equal(prefix, parsed);
        Assert.False(WebApiKeyFormat.TryParse(key + "x", out _));
        Assert.False(WebApiKeyFormat.TryParse(key.Replace("wl_", "xx_", StringComparison.Ordinal), out _));
        Assert.False(WebApiKeyFormat.TryParse(key[..^1] + "!", out _));
        Assert.Equal(64, WebApiKeyFormat.Hash(key).Length);
    }

    [Theory]
    [InlineData("a@b.co", true)]
    [InlineData("first.last+tag@example.test", true)]
    [InlineData("no-at.example", false)]
    [InlineData("two@@example.test", false)]
    [InlineData("a@nodot", false)]
    [InlineData("a b@example.test", false)]
    [InlineData("a@example.test\r\nBcc: x@evil.test", false)]
    [InlineData("<a@example.test>", false)]
    public void EmailValidation(string email, bool ok) => Assert.Equal(ok, WebAccountValidation.IsValidEmail(email));

    [Fact]
    public async Task Throttle_BacksOffExponentially_AndPerIp()
    {
        var clock = new ManualTimeProvider();
        var throttle = new InMemoryLoginThrottle(Config(c =>
        {
            c.Auth.Lockout.FreeAttempts = 2;
            c.Auth.Lockout.MaxFailuresPerIpPerHour = 5;
        }), clock);
        var ip = System.Net.IPAddress.Parse("203.0.113.5");

        await throttle.RecordFailureAsync("alice", ip, default);
        Assert.Null(await throttle.CheckAsync("alice", ip, default));
        await throttle.RecordFailureAsync("alice", ip, default);
        Assert.Equal(TimeSpan.FromSeconds(1), await throttle.CheckAsync("ALICE ", ip, default));
        await throttle.RecordFailureAsync("alice", ip, default);
        Assert.Equal(TimeSpan.FromSeconds(2), await throttle.CheckAsync("alice", ip, default));
        clock.Advance(TimeSpan.FromSeconds(2));
        Assert.Null(await throttle.CheckAsync("alice", ip, default));

        await throttle.RecordFailureAsync("bob", ip, default);
        await throttle.RecordFailureAsync("carol", ip, default);
        Assert.Equal(TimeSpan.FromHours(1), await throttle.CheckAsync("dave", ip, default)); // 5 failures from this IP
    }

    [Fact]
    public async Task SecurityTokens_ArePurposeBound_Expire_AndAreSingleUse()
    {
        using var context = new FakeWebLogicContext();
        var clock = new ManualTimeProvider();
        var users = new InMemoryWebUserStore();
        var user = new WebUser { Id = "u1", UserName = "alice" };
        await users.CreateAsync(user, default);
        var tokens = new WebSecurityTokenService(new EphemeralDataProtectionProvider(), users, new InMemoryOneTimeUseStore(clock), context, clock);

        var token = tokens.Create(WebTokenPurposes.PasswordReset, user, TimeSpan.FromMinutes(10), "data");
        Assert.True((await tokens.ValidateAsync(WebTokenPurposes.MagicLink, token, false, default)).IsFailure);
        var valid = await tokens.ValidateAsync(WebTokenPurposes.PasswordReset, token, false, default);
        Assert.Equal("data", valid.Value!.Data);

        Assert.True((await tokens.ValidateAsync(WebTokenPurposes.PasswordReset, token, true, default)).IsSuccess);
        Assert.True((await tokens.ValidateAsync(WebTokenPurposes.PasswordReset, token, true, default)).IsFailure);

        var later = tokens.Create(WebTokenPurposes.PasswordReset, user, TimeSpan.FromMinutes(10));
        clock.Advance(TimeSpan.FromMinutes(11));
        Assert.Equal("weblogic.auth.token_invalid", (await tokens.ValidateAsync(WebTokenPurposes.PasswordReset, later, false, default)).Error!.Code);

        var stamped = tokens.Create(WebTokenPurposes.PasswordReset, user, TimeSpan.FromMinutes(10));
        await users.UpdateAsync(user with { SecurityStamp = WebUser.NewStamp() }, default);
        Assert.True((await tokens.ValidateAsync(WebTokenPurposes.PasswordReset, stamped, false, default)).IsFailure);
        Assert.True((await tokens.ValidateAsync(WebTokenPurposes.PasswordReset, "garbage", false, default)).IsFailure);
    }

    private sealed class PlainLegacy : IWebPasswordAlgorithm
    {
        public string Id => "legacy";

        public WebPasswordVerification Verify(string parameters, string payload, string password) =>
            payload == password ? WebPasswordVerification.Success : WebPasswordVerification.Failed;
    }
}

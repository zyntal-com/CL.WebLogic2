using System.Text.Json;
using CL.WebLogic.ExternalLogins.Jwt;

namespace CL.WebLogic.ExternalLogins.Tests;

public sealed class JwtValidatorTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
    private static readonly string[] Crit = ["x"];
    private static readonly string[] TwoAudiences = ["client-1", "client-2"];
    private readonly TestSigner _signer = new();

    private IReadOnlyList<WebJsonWebKey> Keys() =>
        [.. JsonDocument.Parse(_signer.Jwks()).RootElement.GetProperty("keys").EnumerateArray().Select(WebJsonWebKey.FromJson).OfType<WebJsonWebKey>()];

    private static WebJwtValidationParameters Parameters(string issuer = "https://idp.test", string? nonce = "n-1") =>
        new() { Issuer = issuer, Audience = "client-1", Nonce = nonce, Now = Now };

    private static Dictionary<string, object> Claims(Action<Dictionary<string, object>>? change = null)
    {
        var claims = new Dictionary<string, object>
        {
            ["iss"] = "https://idp.test", ["aud"] = "client-1", ["sub"] = "user-42", ["nonce"] = "n-1",
            ["iat"] = Now.AddMinutes(-1).ToUnixTimeSeconds(), ["exp"] = Now.AddMinutes(10).ToUnixTimeSeconds()
        };
        change?.Invoke(claims);
        return claims;
    }

    [Theory]
    [InlineData("RS256")]
    [InlineData("PS256")]
    [InlineData("ES256")]
    public void Valid_tokens_pass_with_every_supported_algorithm(string alg)
    {
        var (claims, error) = WebJwtValidator.Validate(_signer.Sign(Claims(), alg), Keys(), Parameters());
        Assert.Null(error);
        Assert.Equal("user-42", claims!.Value.GetProperty("sub").GetString());
    }

    [Theory]
    [InlineData("none")]
    [InlineData("HS256")] // the classic confusion attack: HMAC keyed with the public RSA key
    [InlineData("RS999")]
    public void Unsafe_algorithms_are_refused(string alg) =>
        Assert.NotNull(WebJwtValidator.Validate(_signer.Sign(Claims(), alg, kid: _signer.Kid), Keys(), Parameters()).Error);

    [Fact]
    public void A_tampered_token_or_a_foreign_key_fails()
    {
        var token = _signer.Sign(Claims());
        var parts = token.Split('.');
        var forged = TestSigner.B64(JsonSerializer.SerializeToUtf8Bytes(Claims(c => c["sub"] = "admin"))) ;
        Assert.Contains("signature", WebJwtValidator.Validate(parts[0] + "." + forged + "." + parts[2], Keys(), Parameters()).Error);

        using var other = new TestSigner();
        Assert.NotNull(WebJwtValidator.Validate(other.Sign(Claims()), Keys(), Parameters()).Error);
        Assert.Equal("unknown signing key", WebJwtValidator.Validate(_signer.Sign(Claims(), kid: "rotated"), Keys(), Parameters()).Error);
        Assert.Equal("malformed token", WebJwtValidator.Validate("a.b", Keys(), Parameters()).Error);
        Assert.Equal("unsupported critical header", WebJwtValidator.Validate(_signer.Sign(Claims(), extraHeader: new { crit = Crit }), Keys(), Parameters()).Error);
    }

    [Fact]
    public void Claims_are_checked()
    {
        string? Check(Action<Dictionary<string, object>> change, WebJwtValidationParameters? parameters = null) =>
            WebJwtValidator.Validate(_signer.Sign(Claims(change)), Keys(), parameters ?? Parameters()).Error;

        Assert.Equal("wrong issuer", Check(c => c["iss"] = "https://evil.test"));
        Assert.Equal("wrong audience", Check(c => c["aud"] = "client-2"));
        Assert.Equal("wrong authorized party", Check(c => c["aud"] = TwoAudiences));
        Assert.Null(Check(c => { c["aud"] = TwoAudiences; c["azp"] = "client-1"; }));
        Assert.Equal("wrong authorized party", Check(c => c["azp"] = "client-2"));
        Assert.Equal("expired", Check(c => c["exp"] = Now.AddMinutes(-6).ToUnixTimeSeconds()));
        Assert.Null(Check(c => c["exp"] = Now.AddMinutes(-4).ToUnixTimeSeconds())); // within the clock skew
        Assert.Equal("expired", Check(c => c.Remove("exp")));
        Assert.Equal("not yet valid", Check(c => c["nbf"] = Now.AddMinutes(10).ToUnixTimeSeconds()));
        Assert.Equal("issued in the future", Check(c => c["iat"] = Now.AddMinutes(10).ToUnixTimeSeconds()));
        Assert.Equal("wrong nonce", Check(c => c["nonce"] = "n-2"));
        Assert.Equal("wrong nonce", Check(c => c.Remove("nonce")));
        Assert.Null(Check(c => c.Remove("nonce"), Parameters(nonce: null)));
    }

    [Fact]
    public void A_multi_tenant_issuer_takes_the_tenant_from_the_token()
    {
        var parameters = Parameters(issuer: "https://login.microsoftonline.com/{tenantid}/v2.0");
        var token = _signer.Sign(Claims(c => { c["tid"] = "tenant-a"; c["iss"] = "https://login.microsoftonline.com/tenant-a/v2.0"; }));
        Assert.Null(WebJwtValidator.Validate(token, Keys(), parameters).Error);

        var mismatch = _signer.Sign(Claims(c => { c["tid"] = "tenant-a"; c["iss"] = "https://login.microsoftonline.com/tenant-b/v2.0"; }));
        Assert.Equal("wrong issuer", WebJwtValidator.Validate(mismatch, Keys(), parameters).Error);
        Assert.Equal("wrong issuer", WebJwtValidator.Validate(_signer.Sign(Claims()), Keys(), parameters).Error); // no tid
    }

    [Fact]
    public void Weak_and_unusable_keys_are_ignored()
    {
        using var weak = new TestSigner("weak", rsaBits: 1024);
        var keys = JsonDocument.Parse(weak.Jwks()).RootElement.GetProperty("keys").EnumerateArray().Select(WebJsonWebKey.FromJson).OfType<WebJsonWebKey>().ToList();
        Assert.DoesNotContain(keys, k => k.KeyId == "weak");
        Assert.Null(WebJsonWebKey.FromJson(JsonDocument.Parse("""{"kty":"oct","k":"c2VjcmV0"}""").RootElement));
        Assert.Null(WebJsonWebKey.FromJson(JsonDocument.Parse("""{"kty":"RSA","use":"enc","n":"AQAB","e":"AQAB"}""").RootElement));

        var restricted = WebJsonWebKey.FromJson(JsonDocument.Parse(
            $$"""{"kty":"RSA","kid":"{{_signer.Kid}}","alg":"PS256","n":"{{TestSigner.B64(_signer.Rsa.ExportParameters(false).Modulus!)}}","e":"AQAB"}""").RootElement)!;
        Assert.NotNull(WebJwtValidator.Validate(_signer.Sign(Claims(), "RS256"), [restricted], Parameters()).Error);
        Assert.Null(WebJwtValidator.Validate(_signer.Sign(Claims(), "PS256"), [restricted], Parameters()).Error);
    }

    public void Dispose() => _signer.Dispose();
}

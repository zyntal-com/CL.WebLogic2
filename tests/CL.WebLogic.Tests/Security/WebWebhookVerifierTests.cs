using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CL.WebLogic.Security.Webhooks;
using CL.WebLogic.Testing.Services;

namespace CL.WebLogic.Tests.Security;

public sealed class WebWebhookVerifierTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);
    private static readonly byte[] Body = Encoding.UTF8.GetBytes("""{"id":1}""");
    private readonly WebWebhookVerifier _verifier = new(new ManualTimeProvider(Now));

    [Fact]
    public void Hex_WithAndWithoutPrefix_IsAccepted_CaseInsensitive()
    {
        var options = new WebWebhookOptions { Secrets = ["s"], SignaturePrefix = "sha256=" };
        var mac = Mac("s", Body);

        Assert.True(_verifier.Verify(Body, "sha256=" + Convert.ToHexStringLower(mac), null, options).IsSuccess);
        Assert.True(_verifier.Verify(Body, Convert.ToHexString(mac), null, options).IsSuccess);
    }

    [Fact]
    public void Base64_IsAccepted()
    {
        var options = new WebWebhookOptions { Secrets = ["s"], Encoding = WebWebhookSignatureEncoding.Base64 };

        Assert.True(_verifier.Verify(Body, Convert.ToBase64String(Mac("s", Body)), null, options).IsSuccess);
    }

    [Theory]
    [InlineData("not-hex")]
    [InlineData("abc")]
    [InlineData("sha256=")]
    public void GarbageSignature_IsBadOrMissing(string header)
    {
        var options = new WebWebhookOptions { Secrets = ["s"], SignaturePrefix = "sha256=" };

        var result = _verifier.Verify(Body, header, null, options);

        Assert.True(result.IsFailure);
        Assert.Equal("weblogic.webhook.missing_signature", result.Error!.Code);
    }

    [Fact]
    public void WrongSecret_IsBadSignature()
    {
        var options = new WebWebhookOptions { Secrets = ["s"] };

        Assert.Equal("weblogic.webhook.bad_signature", _verifier.Verify(Body, Convert.ToHexString(Mac("other", Body)), null, options).Error!.Code);
    }

    [Fact]
    public void SeparateTimestampHeader_SlackStyle_IsSigned_AndChecked()
    {
        var options = new WebWebhookOptions
        {
            Secrets = ["s"],
            SignaturePrefix = "v0=",
            TimestampHeader = "X-Slack-Request-Timestamp",
            SignedTimestampFormat = "v0:{timestamp}:"
        };
        var ts = Now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var signature = "v0=" + Convert.ToHexStringLower(Mac("s", Encoding.UTF8.GetBytes("v0:" + ts + ":").Concat(Body).ToArray()));

        Assert.True(_verifier.Verify(Body, signature, ts, options).IsSuccess);
        Assert.Equal("weblogic.webhook.bad_timestamp", _verifier.Verify(Body, signature, null, options).Error!.Code);
        Assert.Equal("weblogic.webhook.bad_timestamp", _verifier.Verify(Body, signature, "-5", options).Error!.Code);
        var future = Now.AddMinutes(10).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        Assert.Equal("weblogic.webhook.expired", _verifier.Verify(Body, signature, future, options).Error!.Code);
    }

    [Fact]
    public void NoSecrets_IsAProgrammerError() =>
        Assert.Throws<InvalidOperationException>(() => _verifier.Verify(Body, "00", null, new WebWebhookOptions()));

    private static byte[] Mac(string secret, byte[] payload) => HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), payload);
}

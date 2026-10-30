using CL.WebLogic.Diagnostics.Services;

namespace CL.WebLogic.Tests.Diagnostics;

public sealed class WebMetricTagsTests
{
    [Theory]
    [InlineData("country-denied:DK", "country-denied")]
    [InlineData("forbidden:group:admin", "forbidden")]
    [InlineData("route-rate-limit:tiny", "route-rate-limit")]
    [InlineData("csrf-token-missing", "csrf-token-missing")]
    [InlineData(null, "rejected")]
    [InlineData("", "rejected")]
    [InlineData("Blocked 10.0.0.1", "other")]
    [InlineData("user@example.test", "other")]
    public void ReasonCode_DropsTheVariablePart(string? reason, string expected) =>
        Assert.Equal(expected, WebMetricTags.ReasonCode(reason));

    [Theory]
    [InlineData(200, "2xx")]
    [InlineData(302, "3xx")]
    [InlineData(404, "4xx")]
    [InlineData(503, "5xx")]
    [InlineData(0, "other")]
    public void StatusClass_GroupsCodes(int status, string expected) =>
        Assert.Equal(expected, WebMetricTags.StatusClass(status));

    [Theory]
    [InlineData("get", "GET")]
    [InlineData("POST", "POST")]
    [InlineData("PROPFIND", "_OTHER")]
    public void Method_KeepsOnlyStandardMethods(string method, string expected) =>
        Assert.Equal(expected, WebMetricTags.Method(method));

    [Fact]
    public void Methods_KeepKnownMethods_AndProviders_Only()
    {
        Assert.Equal("pwd,otp", WebMetricTags.Methods(["pwd", "otp"]));
        Assert.Equal("ext:google", WebMetricTags.Methods(["ext:google"]));
        Assert.Equal("other", WebMetricTags.Methods(["alice@example.test"]));
        Assert.Equal("other", WebMetricTags.Methods(["ext:Some Provider!"]));
        Assert.Equal("none", WebMetricTags.Methods([]));
    }
}

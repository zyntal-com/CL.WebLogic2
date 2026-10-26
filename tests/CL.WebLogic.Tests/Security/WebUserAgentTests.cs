using CL.WebLogic.Security.Sessions;

namespace CL.WebLogic.Tests.Security;

public sealed class WebUserAgentTests
{
    private const string Chrome120 = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
    private const string Chrome131 = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.6778.86 Safari/537.36";
    private const string Edge = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36 Edg/131.0.2903.70";
    private const string SafariMac = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.1 Safari/605.1.15";
    private const string SafariIPhone = "Mozilla/5.0 (iPhone; CPU iPhone OS 17_1 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.1 Mobile/15E148 Safari/604.1";
    private const string FirefoxLinux = "Mozilla/5.0 (X11; Linux x86_64; rv:121.0) Gecko/20100101 Firefox/121.0";
    private const string ChromeAndroid = "Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Mobile Safari/537.36";

    [Theory]
    [InlineData(Chrome120, "Chrome on Windows")]
    [InlineData(Edge, "Edge on Windows")]
    [InlineData(SafariMac, "Safari on macOS")]
    [InlineData(SafariIPhone, "Safari on iOS")]
    [InlineData(FirefoxLinux, "Firefox on Linux")]
    [InlineData(ChromeAndroid, "Chrome on Android")]
    public void Common_browsers_are_named_by_browser_and_system(string userAgent, string expected) =>
        Assert.Equal(expected, WebUserAgent.Describe(userAgent));

    [Fact]
    public void A_browser_update_is_the_same_device_but_another_browser_is_not()
    {
        Assert.Equal(WebUserAgent.DeviceKey(Chrome120), WebUserAgent.DeviceKey(Chrome131));
        Assert.NotEqual(WebUserAgent.DeviceKey(Chrome131), WebUserAgent.DeviceKey(Edge));
        Assert.NotEqual(WebUserAgent.DeviceKey(ChromeAndroid), WebUserAgent.DeviceKey(Chrome131));
    }

    [Fact]
    public void Unrecognised_agents_compare_without_their_version_numbers()
    {
        Assert.Null(WebUserAgent.Describe("MyApp/1.2"));
        Assert.Equal(WebUserAgent.DeviceKey("MyApp/1.2 (build 77)"), WebUserAgent.DeviceKey("MyApp/1.3 (build 81)"));
        Assert.NotEqual(WebUserAgent.DeviceKey("MyApp/1.2"), WebUserAgent.DeviceKey("OtherApp/1.2"));
        Assert.Equal("", WebUserAgent.DeviceKey(null));
    }
}

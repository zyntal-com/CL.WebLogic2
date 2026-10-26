using System.Net;
using CL.WebLogic.Configuration.Models;
using CL.WebLogic.Security.Models;
using CL.WebLogic.Security.Rules;

namespace CL.WebLogic.Tests.Security;

public sealed class WebIpRuleSetTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Config_and_provider_rules_merge_with_ids()
    {
        var config = new WebLogicConfig();
        config.Security.IpRules.Deny.Add("198.51.100.0/24");
        config.Security.IpRules.DenyCountries.Add("XX");
        var set = WebIpRuleSet.Compile(WebSecuritySettings.Compile(config, 1), [
            new WebIpRule { Id = "ban-1", Network = "203.0.113.7" },
            new WebIpRule { Id = "geo-1", Country = "YY" },
            new WebIpRule { Id = "office", Network = "192.0.2.0/24", Action = WebIpRuleAction.Exempt }
        ], Now);

        Assert.Equal("config.deny[0]", set.MatchDeny(IPAddress.Parse("198.51.100.9")));
        Assert.Equal("ban-1", set.MatchDeny(IPAddress.Parse("::ffff:203.0.113.7"))); // IPv4-mapped
        Assert.Null(set.MatchDeny(IPAddress.Parse("203.0.113.8")));
        Assert.Equal("office", set.MatchExempt(IPAddress.Parse("192.0.2.10")));
        Assert.Equal("config.denyCountries", set.MatchDenyCountry("xx"));
        Assert.Equal("geo-1", set.MatchDenyCountry("YY"));
        Assert.False(set.HasAllowList);
    }

    [Fact]
    public void Invalid_and_expired_rules_are_skipped_and_the_next_expiry_bounds_the_set()
    {
        var invalid = new List<string>();
        var set = WebIpRuleSet.Compile(WebSecuritySettings.Compile(new WebLogicConfig(), 1), [
            new WebIpRule { Id = "both", Network = "10.0.0.1", Country = "DE" },
            new WebIpRule { Id = "neither" },
            new WebIpRule { Id = "bad-cidr", Network = "10.0.0.0/99" },
            new WebIpRule { Id = "exempt-country", Country = "DE", Action = WebIpRuleAction.Exempt },
            new WebIpRule { Id = "expired", Network = "10.0.0.2", ExpiresAt = Now.AddSeconds(-1) },
            new WebIpRule { Id = "soon", Network = "10.0.0.3", ExpiresAt = Now.AddMinutes(10) }
        ], Now, r => invalid.Add(r.Id));

        Assert.Equal(["both", "neither", "bad-cidr", "exempt-country"], invalid);
        Assert.Null(set.MatchDeny(IPAddress.Parse("10.0.0.2")));
        Assert.Equal("soon", set.MatchDeny(IPAddress.Parse("10.0.0.3")));
        Assert.Equal(Now.AddMinutes(10), set.ValidUntil);
    }
}

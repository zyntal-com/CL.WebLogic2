using CL.WebLogic.Forms.AntiBot;
using CL.WebLogic.Forms.Wizards;
using CL.WebLogic.Testing.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace CL.WebLogic.Tests.Forms;

public sealed class AntiBotAndWizardTests
{
    private static FormCollection Form(params (string Key, string Value)[] pairs) =>
        new(pairs.ToDictionary(p => p.Key, p => new StringValues(p.Value)));

    [Fact]
    public void Timestamp_enforces_minimum_and_maximum_age()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var antiBot = new WebFormAntiBot(new EphemeralDataProtectionProvider(), FormTestServices.Config(), clock);
        var stamp = antiBot.IssueTimestamp("contact");

        Assert.Equal("too-fast", antiBot.Check("contact", Form((WebFormAntiBot.TimestampField, stamp))));
        clock.Advance(TimeSpan.FromSeconds(3));
        Assert.Null(antiBot.Check("contact", Form((WebFormAntiBot.TimestampField, stamp), (WebFormAntiBot.HoneypotField, ""))));
        Assert.Equal("timestamp-invalid", antiBot.Check("signup", Form((WebFormAntiBot.TimestampField, stamp))));
        clock.Advance(TimeSpan.FromHours(9));
        Assert.Equal("expired", antiBot.Check("contact", Form((WebFormAntiBot.TimestampField, stamp))));
    }

    [Fact]
    public void Honeypot_missing_and_tampered_timestamps_are_rejected()
    {
        var antiBot = new WebFormAntiBot(new EphemeralDataProtectionProvider(), FormTestServices.Config(c => c.Forms.MinSubmitSeconds = 0));
        var stamp = antiBot.IssueTimestamp("contact");

        Assert.Equal("honeypot", antiBot.Check("contact", Form((WebFormAntiBot.HoneypotField, "http://spam"), (WebFormAntiBot.TimestampField, stamp))));
        Assert.Equal("timestamp-invalid", antiBot.Check("contact", Form((WebFormAntiBot.TimestampField, stamp + "x"))));
        Assert.Equal("timestamp-invalid", antiBot.Check("contact", Form((WebFormAntiBot.TimestampField, ""))));
        Assert.Null(antiBot.Check("contact", Form((WebFormAntiBot.TimestampField, stamp))));
    }

    [Fact]
    public void Wizard_state_round_trips_and_is_bound_to_the_form()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var wizard = new WebWizardState(new EphemeralDataProtectionProvider(), clock);
        var token = wizard.Protect("signup", 1, FormTestServices.Values(("Email", "a@b.co"), ("Tags", "x"), ("Tags", "y")));

        var state = wizard.Read("signup", token);
        Assert.NotNull(state);
        Assert.Equal(1, state.Value.Step);
        Assert.Equal("a@b.co", state.Value.Values["Email"]);
        Assert.Equal("x,y", state.Value.Values["Tags"].ToString());

        Assert.Null(wizard.Read("other", token));
        // Change a character in the middle: the last ones may only carry padding bits (or already be "zz").
        var middle = token.Length / 2;
        Assert.Null(wizard.Read("signup", token[..middle] + (token[middle] == 'A' ? 'B' : 'A') + token[(middle + 1)..]));
        Assert.Null(wizard.Read("signup", null));
        clock.Advance(TimeSpan.FromHours(9));
        Assert.Null(wizard.Read("signup", token));
    }
}

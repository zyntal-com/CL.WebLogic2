using System.Collections.Concurrent;
using CL.WebLogic.Auth.Accounts;
using CL.WebLogic.Auth.Notifications;
using CL.WebLogic.Auth.TwoFactor;
using CL.WebLogic.Auth.Users;
using CL.WebLogic.IntegrationTests.Security;
using CL.WebLogic.Security.Audit;
using Microsoft.Extensions.DependencyInjection;

namespace CL.WebLogic.IntegrationTests.Auth;

/// <summary>Account-security changes reach the audit log, with the request when there is one.</summary>
public sealed class AccountAuditTests
{
    [Fact]
    public async Task Password_change_in_a_request_is_audited_with_the_request()
    {
        var sink = new CollectingSink();
        await using var host = await AuthTestSite.StartAsync(web: w => w.Services.AddSingletonSink(sink));
        var user = await AuthTestSite.CreateUserAsync(host);
        await AuthTestSite.LoginAsync(host.Client);

        Assert.Equal("Changed", await AuthTestSite.PostAsync(host.Client, "/auth/change-password", new { current = AuthTestSite.Password, newPassword = "a brand new passphrase" }));

        var entry = await sink.WaitAsync(e => e.Category == "auth.password");
        Assert.Equal("changed", entry.Outcome);
        Assert.Equal(user.Id, entry.Data["user"]);
        Assert.Equal("/auth/change-password", entry.Path);
        Assert.Equal(64, entry.IpHash?.Length);
    }

    [Fact]
    public async Task Changes_outside_a_request_are_audited_with_the_user_only()
    {
        var sink = new CollectingSink();
        await using var host = await AuthTestSite.StartAsync(web: w => w.Services.AddSingletonSink(sink));
        var accounts = host.Services.GetRequiredService<WebAccountService>();
        var twoFactor = host.Services.GetRequiredService<WebTwoFactorService>();
        var user = await AuthTestSite.CreateUserAsync(host);

        Assert.Equal("registered", (await sink.WaitAsync(e => e.Category == "auth.account")).Outcome);

        await AuthTestSite.EnableTwoFactorAsync(host, user);
        Assert.Equal("enabled", (await sink.WaitAsync(e => e.Category == "auth.two-factor")).Outcome);
        await twoFactor.RegenerateRecoveryCodesAsync(user, default);
        Assert.NotNull(await sink.WaitAsync(e => e.Outcome == "recovery-codes-regenerated"));
        await twoFactor.DisableAsync(user, default);
        Assert.NotNull(await sink.WaitAsync(e => e.Category == "auth.two-factor" && e.Outcome == "disabled"));
        // Turning two-factor on creates the first codes: that is not a regeneration.
        Assert.Single(sink.Entries, e => e.Outcome == "recovery-codes-regenerated");

        user = (await host.Services.GetRequiredService<IWebUserStore>().FindByIdAsync(user.Id, default))!;
        var requested = await accounts.RequestEmailChangeAsync(user, "alice.new@example.test", default);
        Assert.True(requested.IsSuccess, requested.Error?.Message);
        var mail = await AuthTestSite.WaitForMailAsync(host, WebSecurityNotificationKind.EmailChangeConfirmation);
        Assert.True((await accounts.ConfirmEmailChangeAsync(AuthTestSite.TokenFrom(mail), default)).IsSuccess);
        var changed = await sink.WaitAsync(e => e.Category == "auth.email");
        Assert.Equal("changed", changed.Outcome);
        Assert.Equal(user.Id, changed.UserId);
        Assert.Null(changed.IpHash);
    }

    private sealed class CollectingSink : IWebAuditSink
    {
        private readonly ConcurrentQueue<WebAuditEntry> _entries = new();

        public IReadOnlyList<WebAuditEntry> Entries => [.. _entries];

        public ValueTask WriteAsync(IReadOnlyList<WebAuditEntry> entries, CancellationToken cancellationToken)
        {
            foreach (var entry in entries)
                _entries.Enqueue(entry);
            return ValueTask.CompletedTask;
        }

        public async Task<WebAuditEntry> WaitAsync(Func<WebAuditEntry, bool> match)
        {
            var until = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < until)
            {
                if (_entries.FirstOrDefault(match) is { } entry)
                    return entry;
                await Task.Delay(20);
            }
            throw new Xunit.Sdk.XunitException("The expected audit entry did not arrive.");
        }
    }
}

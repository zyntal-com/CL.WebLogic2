using CL.WebLogic.Configuration.Models;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Realtime.Models;
using CL.WebLogic.Realtime.Services;
using CL.WebLogic.Testing.Services;
using Microsoft.AspNetCore.DataProtection;

namespace CL.WebLogic.Tests.Realtime;

public sealed class RealtimePrimitivesTests
{
    private static readonly WebRealtimeIdentity Ada = new() { UserId = "ada", TenantId = "t1", Groups = ["editors"], Permissions = ["posts.*"] };
    private static readonly WebRealtimeIdentity Operator = new() { UserId = "op", Permissions = ["weblogic.internal"], IsInternal = true };

    private static (WebRealtimeHub Hub, FakeWebLogicContext Context, ManualTimeProvider Clock) CreateHub(Action<RealtimeConfig>? configure = null)
    {
        var config = new WebLogicConfig();
        configure?.Invoke(config.Realtime);
        var context = new FakeWebLogicContext(config);
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        return (new WebRealtimeHub(new InProcessRealtimeBackplane(), context.Config, context, clock), context, clock);
    }

    private static WebRealtimeConnection Connect(WebRealtimeHub hub, WebRealtimeIdentity identity, string ip = "10.0.0.1", params string[] channels) =>
        hub.Connect(identity, channels.ToHashSet(), WebRealtimeTransport.ServerSentEvents, ip, out _)!;

    private static List<string> Drain(WebRealtimeConnection connection)
    {
        var types = new List<string>();
        var reader = typeof(WebRealtimeConnection).GetProperty("Reader", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(connection) as System.Threading.Channels.ChannelReader<WebRealtimeEnvelope>;
        while (reader!.TryRead(out var envelope))
            types.Add(envelope.Type);
        return types;
    }

    [Theory]
    [InlineData(WebRealtimeAudienceKind.Public, null, true, true, true)]
    [InlineData(WebRealtimeAudienceKind.Authenticated, null, false, true, true)]
    [InlineData(WebRealtimeAudienceKind.Group, "editors", false, true, false)]
    [InlineData(WebRealtimeAudienceKind.Permission, "posts.edit", false, true, false)]
    [InlineData(WebRealtimeAudienceKind.Permission, "users.edit", false, false, false)]
    [InlineData(WebRealtimeAudienceKind.User, "ada", false, true, false)]
    [InlineData(WebRealtimeAudienceKind.Tenant, "t1", false, true, false)]
    [InlineData(WebRealtimeAudienceKind.Internal, null, false, false, true)]
    public void Audiences_decide_who_receives(WebRealtimeAudienceKind kind, string? value, bool anonymous, bool ada, bool op)
    {
        var (hub, context, _) = CreateHub();
        using (context)
        using (hub)
        {
            var envelope = new WebRealtimeEnvelope("1", "t", null, new WebRealtimeAudience(kind, value), "{}", DateTimeOffset.UtcNow);
            Assert.Equal(anonymous, Connect(hub, WebRealtimeIdentity.Anonymous).Accepts(envelope));
            Assert.Equal(ada, Connect(hub, Ada).Accepts(envelope));
            Assert.Equal(op, Connect(hub, Operator).Accepts(envelope));
        }
    }

    [Fact]
    public async Task Tenant_messages_only_reach_that_tenant_whatever_the_audience()
    {
        // Regression (security review): Group/Authenticated audiences crossed tenants.
        var (hub, context, _) = CreateHub();
        using (context)
        using (hub)
        {
            var other = new WebRealtimeIdentity { UserId = "bob", TenantId = "t2", Groups = ["editors"] };
            var inT1 = Connect(hub, Ada);
            var inT2 = Connect(hub, other);
            await hub.PublishToTenantAsync("t1", "t1.only", null, WebRealtimeAudience.Group("editors"));
            await hub.PublishAsync("global", null, WebRealtimeAudience.Group("editors"));

            Assert.Equal(["t1.only", "global"], Drain(inT1));
            Assert.Equal(["global"], Drain(inT2));
            Assert.DoesNotContain(hub.Replay(Connect(hub, other), null), e => e.Type == "t1.only");
        }
    }

    [Fact]
    public async Task Hub_delivers_by_audience_and_channel()
    {
        var (hub, context, _) = CreateHub();
        using (context)
        using (hub)
        {
            var anonymous = Connect(hub, WebRealtimeIdentity.Anonymous);
            var ada = Connect(hub, Ada, "10.0.0.2", "news");
            await hub.PublishAsync("public", null, WebRealtimeAudience.Public);
            await hub.PublishAsync("for-ada", new { secret = 1 }, WebRealtimeAudience.User("ada"));
            await hub.PublishAsync("sports", null, WebRealtimeAudience.Public, "sports");
            await hub.PublishAsync("news", null, WebRealtimeAudience.Public, "news");

            Assert.Equal(["public", "sports", "news"], Drain(anonymous));
            Assert.Equal(["public", "for-ada", "news"], Drain(ada));
        }
    }

    [Fact]
    public async Task Replay_only_returns_messages_the_connection_may_see()
    {
        // Regression: the old hub replayed every buffered message regardless of audience.
        var (hub, context, _) = CreateHub();
        using (context)
        using (hub)
        {
            var watcher = Connect(hub, Ada);
            await hub.PublishAsync("first", null, WebRealtimeAudience.Public);
            Assert.Empty(hub.Replay(watcher, null)); // no resume point, nothing to replay
            await hub.PublishAsync("private", null, WebRealtimeAudience.User("ada"));
            await hub.PublishAsync("internal", null, WebRealtimeAudience.Internal);
            await hub.PublishAsync("last", null, WebRealtimeAudience.Public);

            Assert.Equal(["first", "private", "last"], Drain(watcher));
            var anonymous = Connect(hub, WebRealtimeIdentity.Anonymous, "10.0.0.9");
            var start = typeof(WebRealtimeHub).GetField("_node", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(hub) + "-1";

            Assert.Equal(["last"], hub.Replay(anonymous, start).Select(e => e.Type));
            Assert.Equal(["private", "last"], hub.Replay(Connect(hub, Ada, "10.0.0.3"), start).Select(e => e.Type));
            Assert.Equal([WebRealtimeHub.ReplayGapType], hub.Replay(anonymous, "unknown-id").Select(e => e.Type));
        }
    }

    [Fact]
    public void Connection_limits_are_enforced_per_client_and_total()
    {
        var (hub, context, _) = CreateHub(c =>
        {
            c.MaxConnectionsPerClient = 2;
            c.MaxConnections = 3;
        });
        using (context)
        using (hub)
        {
            var first = Connect(hub, WebRealtimeIdentity.Anonymous, "1.1.1.1");
            Connect(hub, WebRealtimeIdentity.Anonymous, "1.1.1.1");
            Assert.Null(hub.Connect(WebRealtimeIdentity.Anonymous, new HashSet<string>(), WebRealtimeTransport.WebSocket, "1.1.1.1", out var perClient));
            Assert.Equal("max-connections-per-client", perClient);

            Connect(hub, WebRealtimeIdentity.Anonymous, "2.2.2.2");
            Assert.Null(hub.Connect(WebRealtimeIdentity.Anonymous, new HashSet<string>(), WebRealtimeTransport.WebSocket, "3.3.3.3", out var total));
            Assert.Equal("max-connections", total);

            hub.Disconnect(first);
            Assert.NotNull(hub.Connect(WebRealtimeIdentity.Anonymous, new HashSet<string>(), WebRealtimeTransport.WebSocket, "1.1.1.1", out _));
        }
    }

    [Fact]
    public void Regression_IPv6_clients_count_per_network_prefix()
    {
        // Security review: each address of one /64 got its own allowance.
        var (hub, context, _) = CreateHub(c => c.MaxConnectionsPerClient = 2);
        using (context)
        using (hub)
        {
            Connect(hub, WebRealtimeIdentity.Anonymous, "2001:db8:1:1::1");
            Connect(hub, WebRealtimeIdentity.Anonymous, "2001:db8:1:1::2");
            Assert.Null(hub.Connect(WebRealtimeIdentity.Anonymous, new HashSet<string>(), WebRealtimeTransport.WebSocket, "2001:db8:1:1::3", out var rejection));
            Assert.Equal("max-connections-per-client", rejection);
            Assert.NotNull(hub.Connect(WebRealtimeIdentity.Anonymous, new HashSet<string>(), WebRealtimeTransport.WebSocket, "2001:db8:1:2::1", out _));
        }
    }

    [Fact]
    public void Closing_a_user_closes_only_their_connections()
    {
        var (hub, context, _) = CreateHub();
        using (context)
        using (hub)
        {
            var ada = Connect(hub, Ada);
            var other = Connect(hub, Operator, "10.0.0.5");

            Assert.Equal(1, hub.CloseUser("ada"));
            Assert.True(ada.Closed.IsCancellationRequested);
            Assert.False(other.Closed.IsCancellationRequested);
            Assert.Equal(1, hub.ConnectionCount);
        }
    }

    [Fact]
    public void Replay_buffer_is_bounded_by_count_and_age()
    {
        var buffer = new WebRealtimeReplayBuffer();
        var now = DateTimeOffset.UtcNow;
        for (var i = 1; i <= 5; i++)
            buffer.Add(new WebRealtimeEnvelope(i.ToString(System.Globalization.CultureInfo.InvariantCulture), "t", null, WebRealtimeAudience.Public, "{}", now), 3, TimeSpan.FromMinutes(5), now);

        Assert.Equal(3, buffer.Count);
        Assert.False(buffer.After("1").Found);
        Assert.Equal(["4", "5"], buffer.After("3").Messages.Select(m => m.Id));

        buffer.Add(new WebRealtimeEnvelope("6", "t", null, WebRealtimeAudience.Public, "{}", now.AddMinutes(10)), 3, TimeSpan.FromMinutes(5), now.AddMinutes(10));
        Assert.Equal(1, buffer.Count);
    }

    [Fact]
    public void Tickets_round_trip_and_expire()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var tickets = new WebRealtimeTickets(new EphemeralDataProtectionProvider(), new WebLogicConfigHolder(new WebLogicConfig()), clock);
        var (ticket, expires) = tickets.Issue(Ada with { ApiKeyId = "k1" });

        var read = tickets.Read(ticket);
        Assert.Equal("ada", read!.UserId);
        Assert.Equal("k1", read.ApiKeyId); // a ticket minted from a key closes with it
        Assert.Equal(["posts.*"], read.Permissions);
        Assert.Equal(clock.GetUtcNow().AddSeconds(60).ToUnixTimeSeconds(), expires.ToUnixTimeSeconds());
        Assert.Null(tickets.Read(ticket + "x"));
        Assert.Null(tickets.Read(null));
        clock.Advance(TimeSpan.FromSeconds(61));
        Assert.Null(tickets.Read(ticket));
    }

    [Theory]
    [InlineData("posts.updated", true)]
    [InlineData("a:b-c_d", true)]
    [InlineData("has space", false)]
    [InlineData("", false)]
    public void Message_names_are_validated(string name, bool valid) => Assert.Equal(valid, WebRealtimeHub.IsValidName(name));
}

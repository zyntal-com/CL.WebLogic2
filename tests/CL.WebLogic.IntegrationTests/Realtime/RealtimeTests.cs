using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using CL.WebLogic.Auth.Events;
using CL.WebLogic.IntegrationTests.Security;
using CL.WebLogic.Realtime.Models;
using CL.WebLogic.Realtime.Services;
using CL.WebLogic.Testing.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CL.WebLogic.IntegrationTests.Realtime;

public sealed class RealtimeTests
{
    private const string Sse = "/_weblogic/realtime/sse";

    private static WebRealtimeHub Hub(WebLogicTestHost host) => host.Services.GetRequiredService<WebRealtimeHub>();

    [Fact]
    public async Task Sse_delivers_only_what_the_connection_may_see()
    {
        await using var host = await SecurityTestSite.StartAsync();
        await using var anonymous = await SseStream.OpenAsync(host.Client, Sse);
        var ada = host.CreateClient();
        await ada.GetAsync("/signin?user=ada&groups=editors");
        await using var adaStream = await SseStream.OpenAsync(ada, Sse);

        Assert.Equal("text/event-stream; charset=utf-8", anonymous.Response.Content.Headers.ContentType!.ToString());
        await Hub(host).PublishAsync("hello", new { text = "<hi>" }, WebRealtimeAudience.Public);
        await Hub(host).PublishAsync("private", new { secret = 1 }, WebRealtimeAudience.User("ada"));
        await Hub(host).PublishAsync("editors", null, WebRealtimeAudience.Group("editors"));
        await Hub(host).PublishAsync("done", null, WebRealtimeAudience.Public);

        Assert.Equal(["hello", "done"], await anonymous.TypesUntilAsync("done"));
        Assert.Equal(["hello", "private", "editors", "done"], await adaStream.TypesUntilAsync("done"));
    }

    [Fact]
    public async Task Last_event_id_replays_only_permitted_messages()
    {
        await using var host = await SecurityTestSite.StartAsync();
        string firstId;
        await using (var first = await SseStream.OpenAsync(host.Client, Sse))
        {
            await Hub(host).PublishAsync("a", null, WebRealtimeAudience.Public);
            firstId = (await first.NextAsync())!.Value.Id!;
        }
        await Hub(host).PublishAsync("b-private", null, WebRealtimeAudience.User("someone"));
        await Hub(host).PublishAsync("c", null, WebRealtimeAudience.Public);

        await using var resumed = await SseStream.OpenAsync(host.Client, Sse, firstId);
        Assert.Equal("c", (await resumed.NextAsync())!.Value.Data.GetProperty("type").GetString());

        await using var lost = await SseStream.OpenAsync(host.Client, Sse, "gone-1");
        Assert.Equal(WebRealtimeHub.ReplayGapType, (await lost.NextAsync())!.Value.Data.GetProperty("type").GetString());
    }

    [Fact]
    public async Task Anonymous_connections_and_bad_channels_can_be_refused()
    {
        await using var host = await SecurityTestSite.StartAsync(c => c.Realtime.AllowAnonymous = false);

        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync(Sse)).StatusCode);
        await host.Client.GetAsync("/signin?user=u1");
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.GetAsync(Sse + "?channel=bad%20name")).StatusCode);
    }

    [Fact]
    public async Task Tickets_let_cookieless_clients_connect_as_the_user()
    {
        await using var host = await SecurityTestSite.StartAsync();
        await host.Client.GetAsync("/signin?user=u1");
        var token = await SecurityTestSite.GetCsrfTokenAsync(host.Client);
        var request = new HttpRequestMessage(HttpMethod.Post, "/_weblogic/realtime/ticket");
        request.Headers.Add("X-CSRF-Token", token);
        var ticket = (await (await host.Client.SendAsync(request)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("ticket").GetString()!;

        var cookieless = host.CreateClient();
        await using var stream = await SseStream.OpenAsync(cookieless, Sse + "?ticket=" + Uri.EscapeDataString(ticket));
        await Hub(host).PublishAsync("for-u1", null, WebRealtimeAudience.User("u1"));
        Assert.Equal("for-u1", (await stream.NextAsync())!.Value.Data.GetProperty("type").GetString());

        Assert.Equal(HttpStatusCode.Unauthorized, (await cookieless.GetAsync(Sse + "?ticket=forged")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.CreateClient().PostAsync("/_weblogic/realtime/ticket", null)).StatusCode);
    }

    [Fact]
    public async Task Sign_out_closes_the_users_connections()
    {
        await using var host = await SecurityTestSite.StartAsync();
        await host.Client.GetAsync("/signin?user=u1");
        await using var stream = await SseStream.OpenAsync(host.Client, Sse);
        await using var bystander = await SseStream.OpenAsync(host.CreateClient(), Sse);

        await host.Context.Events.PublishAsync(new UserSignedOutEvent("u1", false));

        Assert.Null(await stream.NextAsync(TimeSpan.FromSeconds(3)));
        Assert.Equal(1, Hub(host).ConnectionCount);
    }

    [Fact]
    public async Task Requests_are_not_broadcast_unless_enabled_and_then_only_to_operators()
    {
        // Regression: the old hub broadcast every request's path and client IP to everyone.
        await using var host = await SecurityTestSite.StartAsync();
        await host.Client.GetAsync("/signin?user=op&perms=weblogic.internal");
        await using var stream = await SseStream.OpenAsync(host.Client, Sse);
        await host.CreateClient().GetAsync("/public");
        await Hub(host).PublishAsync("marker", null, WebRealtimeAudience.Internal);
        Assert.Equal(["marker"], await stream.TypesUntilAsync("marker"));

        await using var enabled = await SecurityTestSite.StartAsync(c => c.Realtime.BroadcastRequests = true);
        await enabled.Client.GetAsync("/signin?user=op&perms=weblogic.internal");
        await using var operatorStream = await SseStream.OpenAsync(enabled.Client, Sse);
        await using var publicStream = await SseStream.OpenAsync(enabled.CreateClient(), Sse);
        await enabled.CreateClient().GetAsync("/public?token=secret");

        var request = (await operatorStream.NextAsync())!.Value.Data;
        Assert.Equal("weblogic.request", request.GetProperty("type").GetString());
        var data = request.GetProperty("data");
        Assert.Equal("/public", data.GetProperty("path").GetString());
        Assert.Equal(200, data.GetProperty("status").GetInt32());
        Assert.DoesNotContain("secret", request.GetRawText());
        Assert.DoesNotContain("127.0.0.1", request.GetRawText());

        await Hub(enabled).PublishAsync("marker", null, WebRealtimeAudience.Public);
        Assert.Equal(["marker"], await publicStream.TypesUntilAsync("marker"));
    }

    [Fact]
    public async Task WebSockets_check_the_origin_and_answer_pings()
    {
        await using var host = await SecurityTestSite.StartAsync();
        var url = new Uri(host.BaseAddress.ToString().Replace("http://", "ws://", StringComparison.Ordinal) + "_weblogic/realtime/ws");

        using (var evil = new ClientWebSocket())
        {
            evil.Options.SetRequestHeader("Origin", "https://evil.test");
            evil.Options.CollectHttpResponseDetails = true;
            await Assert.ThrowsAnyAsync<WebSocketException>(() => evil.ConnectAsync(url, CancellationToken.None));
            Assert.Equal(HttpStatusCode.Forbidden, evil.HttpStatusCode);
        }

        using var socket = new ClientWebSocket();
        socket.Options.SetRequestHeader("Origin", host.BaseAddress.GetLeftPart(UriPartial.Authority));
        await socket.ConnectAsync(url, CancellationToken.None);

        await socket.SendAsync(Encoding.UTF8.GetBytes("{\"type\":\"ping\"}"), WebSocketMessageType.Text, true, CancellationToken.None);
        Assert.Equal("pong", (await ReceiveAsync(socket)).GetProperty("type").GetString());

        await Hub(host).PublishAsync("over-ws", new { n = 1 }, WebRealtimeAudience.Public);
        var message = await ReceiveAsync(socket);
        Assert.Equal("over-ws", message.GetProperty("type").GetString());
        Assert.Equal(1, message.GetProperty("data").GetProperty("n").GetInt32());

        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
    }

    private static async Task<JsonElement> ReceiveAsync(ClientWebSocket socket)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var buffer = new byte[16 * 1024];
        var result = await socket.ReceiveAsync(buffer, cts.Token);
        return JsonDocument.Parse(buffer.AsMemory(0, result.Count)).RootElement.Clone();
    }
}

# Realtime

WebLogic pushes events to browsers over **server-sent events** (`/_weblogic/realtime/sse`) or
**WebSocket** (`/_weblogic/realtime/ws`), with its own hub, written on the shared framework.

## Publishing

```csharp
public sealed class OrderNotifier(WebRealtimeHub hub)
{
    public Task OrderShippedAsync(Order order, CancellationToken ct) =>
        hub.PublishAsync("order.shipped", new { order.Id, order.Number }, WebRealtimeAudience.User(order.CustomerId), cancellationToken: ct);
}
```

| Audience | Who receives it |
|---|---|
| `Public` | Everyone connected (anonymous included, if `realtime.allowAnonymous`) |
| `Authenticated` | Signed-in connections |
| `Group(name)`, `Permission(name)` | Members of a group / holders of a permission |
| `User(id)` | Every connection of one user |
| `Tenant(id)` | Connections of one tenant |
| `Internal` | Holders of `realtime.internalPermission` (operator dashboards) |

In multi-tenant apps use `PublishToTenantAsync(tenantId, ...)`: the event only reaches connections of
that tenant, whatever the audience. Envelopes carry a type, an optional channel, data, an id and a
timestamp; clients can subscribe to channels.

## Bridging CodeLogic events

```csharp
web.BridgeEvent<UserRegisteredEvent>("site.member-joined",
    _ => WebRealtimeAudience.Public,
    _ => new { message = "A new member just joined." });
```

Contributors and plugins can bridge too (`b.BridgeEvent<T>(...)`); the bridge is removed with them.
Choose the data deliberately: only what the audience may see.

## The browser side

```js
document.addEventListener('wl:ready', ({ detail: wl }) => {
  wl.realtime.connect({ transport: 'sse', channels: ['orders'] });
  wl.realtime.subscribe('order.shipped', e => wl.ui.toast(`Order ${e.data.number} shipped`, 'success'));
});
```

With `realtime.clientAutoConnect` the client connects on page load. It reconnects with back-off and
resumes from `Last-Event-ID`; the replay buffer only replays events the connection's audience may see.

## Security

- Connections are re-grouped on sign-in, sign-out and permission changes; signing out closes the user's
  connections.
- WebSocket upgrades check `Origin` against the site and `realtime.allowedOrigins`.
- Cross-origin clients get a short-lived ticket from `/_weblogic/realtime/ticket` (60 s by default,
  `realtime.ticketSeconds`). A ticket can be used more than once inside that window, so keep it short
  and do not log it.
- Limits: total connections, connections per client, per-connection queue (slow consumers are dropped),
  maximum connection time.
- `realtime.broadcastRequests` (off by default) streams request summaries to the `Internal` audience,
  without client IPs.

## Receiving messages

WebSocket clients can send messages. Handle them with an `IWebRealtimeReceiver`
(`web.UseRealtimeReceiver<T>()`); treat every message as untrusted input and check
`connection.Identity` before acting.

## Several nodes

The hub delivers to connections on its own node. Supply an `IWebRealtimeBackplane`
(`web.UseRealtimeBackplane<T>()`: `PublishAsync` + `Subscribe`) on Redis, a database queue or your
message bus, and every node delivers every event. See the [cluster scenario](../scenarios/cluster.md).

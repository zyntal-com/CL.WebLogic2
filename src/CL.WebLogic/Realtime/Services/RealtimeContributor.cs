using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Realtime.Events;
using CL.WebLogic.Realtime.Models;
using CL.WebLogic.Realtime.Transports;
using CL.WebLogic.Results;
using CL.WebLogic.Routing;
using CL.WebLogic.Routing.Models;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Runtime.Models;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Realtime.Services;

/// <summary>
/// Realtime endpoints: <c>GET /_weblogic/realtime/sse</c>, <c>GET /_weblogic/realtime/ws</c>
/// (Origin-checked) and <c>POST /_weblogic/realtime/ticket</c> (signed-in callers).
/// </summary>
public sealed class RealtimeContributor : IWebContributor
{
    /// <summary>Contributor id.</summary>
    public const string ContributorId = "weblogic.realtime";

    private const int MaxChannels = 20;

    private readonly WebRealtimeHub _hub;
    private readonly WebRealtimeTickets _tickets;
    private readonly WebRealtimeIdentityFactory _identities;
    private readonly WebLogicConfigHolder _config;
    private readonly IWebLogicContext _context;
    private readonly TimeProvider _time;

    /// <summary>Creates the contributor.</summary>
    public RealtimeContributor(WebRealtimeHub hub, WebRealtimeTickets tickets, WebRealtimeIdentityFactory identities, WebLogicConfigHolder config, IWebLogicContext context, TimeProvider? time = null)
    {
        _hub = hub ?? throw new ArgumentNullException(nameof(hub));
        _tickets = tickets ?? throw new ArgumentNullException(nameof(tickets));
        _identities = identities ?? throw new ArgumentNullException(nameof(identities));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _time = time ?? TimeProvider.System;
    }

    /// <summary>Descriptor.</summary>
    public static WebContributorDescriptor Descriptor { get; } = new()
    {
        Id = ContributorId,
        Name = "WebLogic realtime",
        Kind = "System",
        Description = "Server-sent events, WebSockets and connection tickets."
    };

    /// <inheritdoc/>
    public Task ContributeAsync(WebContributionBuilder builder, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(builder);
        // Anonymous at the route: anonymous connections only ever receive Public messages, and
        // Realtime.AllowAnonymous can turn them off.
        builder.MapSse("/_weblogic/realtime/sse", (request, ct) => SseAsync(request, ct))
            .AllowAnonymous()
            .WithName("weblogic.realtime.sse")
            .WithDescription("Realtime messages as server-sent events");
        builder.MapApi("/_weblogic/realtime/ws", (request, ct) => WebSocketAsync(request, ct), "GET")
            .AllowAnonymous()
            .WithName("weblogic.realtime.ws")
            .WithDescription("Realtime messages over a WebSocket");
        builder.MapApi("/_weblogic/realtime/ticket", TicketAsync, "POST")
            .WithName("weblogic.realtime.ticket")
            .WithDescription("Issues a short-lived connection ticket");
        return Task.CompletedTask;
    }

    private async ValueTask<WebResult> SseAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        var config = _config.Current.Realtime;
        if (!config.Enabled)
            return WebResult.Status(StatusCodes.Status404NotFound);
        var (connection, refused) = await OpenAsync(request, WebRealtimeTransport.ServerSentEvents, cancellationToken).ConfigureAwait(false);
        return connection is null
            ? refused!
            : new RealtimeSseResult(_hub, connection, LastEventId(request), TimeSpan.FromSeconds(config.HeartbeatSeconds), TimeSpan.FromMinutes(config.MaxConnectionMinutes), _time);
    }

    private async ValueTask<WebResult> WebSocketAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        var config = _config.Current.Realtime;
        if (!config.Enabled || !config.WebSockets)
            return WebResult.Status(StatusCodes.Status404NotFound);
        if (!request.HttpContext.WebSockets.IsWebSocketRequest)
            return WebResult.Problem(StatusCodes.Status400BadRequest, "WebSocket upgrade required");
        if (!OriginAllowed(request))
            return Reject("origin", WebRealtimeTransport.WebSocket, StatusCodes.Status403Forbidden, "Origin not allowed");

        var (connection, refused) = await OpenAsync(request, WebRealtimeTransport.WebSocket, cancellationToken).ConfigureAwait(false);
        return connection is null
            ? refused!
            : new RealtimeWebSocketResult(_hub, connection, LastEventId(request), TimeSpan.FromMinutes(config.MaxConnectionMinutes), _time, _context, config.IncomingMessagesPerMinute);
    }

    private async ValueTask<WebResult> TicketAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        if (!_config.Current.Realtime.Enabled)
            return WebResult.Status(StatusCodes.Status404NotFound);
        var identity = await _identities.CreateAsync(request.Security, cancellationToken).ConfigureAwait(false);
        var (ticket, expires) = _tickets.Issue(identity);
        return WebResult.Json(new { ticket, expiresAt = expires });
    }

    private async ValueTask<(WebRealtimeConnection? Connection, WebResult? Refused)> OpenAsync(WebRequestContext request, WebRealtimeTransport transport, CancellationToken cancellationToken)
    {
        var config = _config.Current.Realtime;
        WebRealtimeIdentity identity;
        var ticket = request.GetQuery("ticket");
        if (ticket is not null)
        {
            var fromTicket = _tickets.Read(ticket);
            if (fromTicket is null)
                return (null, Reject("ticket", transport, StatusCodes.Status401Unauthorized, "The ticket is invalid or expired"));
            identity = fromTicket;
        }
        else
        {
            identity = await _identities.CreateAsync(request.Security, cancellationToken).ConfigureAwait(false);
        }
        if (!identity.IsAuthenticated && !config.AllowAnonymous)
            return (null, Reject("anonymous", transport, StatusCodes.Status401Unauthorized, "Sign in to connect"));

        var requested = request.GetQueryValues("channel");
        if (requested.Count > MaxChannels || requested.Any(c => !WebRealtimeHub.IsValidName(c)))
            return (null, WebResult.Problem(StatusCodes.Status400BadRequest, "Invalid channels"));

        var connection = _hub.Connect(identity, requested.ToHashSet(StringComparer.Ordinal), transport, request.ClientIp, out var rejection);
        return connection is null
            ? (null, Reject(rejection!, transport, StatusCodes.Status429TooManyRequests, "Too many realtime connections"))
            : (connection, null);
    }

    private ProblemResult Reject(string reason, WebRealtimeTransport transport, int status, string title)
    {
        _context.Logger.Info($"Realtime connection refused ({reason}, {transport}).");
        _context.Events.Publish(new RealtimeConnectionRejectedEvent(reason, transport.ToString()));
        return WebResult.Problem(status, title);
    }

    private bool OriginAllowed(WebRequestContext request)
    {
        var origin = request.HttpContext.Request.Headers.Origin.ToString();
        if (origin.Length == 0)
            return true; // not a browser: cross-site WebSocket hijacking needs one
        var http = request.HttpContext.Request;
        var own = http.Scheme + "://" + http.Host.Value;
        var config = _config.Current;
        return string.Equals(origin, own, StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrEmpty(config.PublicBaseUrl) && Uri.TryCreate(config.PublicBaseUrl, UriKind.Absolute, out var site)
                && string.Equals(origin, site.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase))
            || config.Realtime.AllowedOrigins.Any(o => string.Equals(o.TrimEnd('/'), origin, StringComparison.OrdinalIgnoreCase));
    }

    private static string? LastEventId(WebRequestContext request)
    {
        var value = request.GetHeader("Last-Event-ID") ?? request.GetQuery("lastEventId");
        return value is { Length: > 0 and <= 100 } ? value : null;
    }
}

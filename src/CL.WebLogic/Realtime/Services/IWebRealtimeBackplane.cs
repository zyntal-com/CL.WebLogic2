using CL.WebLogic.Realtime.Models;

namespace CL.WebLogic.Realtime.Services;

/// <summary>
/// Carries published messages to every node. The default delivers in-process; clustered apps
/// supply one over their message bus (Redis, a database, …).
/// </summary>
public interface IWebRealtimeBackplane
{
    /// <summary>Publishes to every node (including this one).</summary>
    Task PublishAsync(WebRealtimeEnvelope envelope, CancellationToken cancellationToken);

    /// <summary>Receives messages published on any node.</summary>
    IDisposable Subscribe(Func<WebRealtimeEnvelope, ValueTask> handler);
}

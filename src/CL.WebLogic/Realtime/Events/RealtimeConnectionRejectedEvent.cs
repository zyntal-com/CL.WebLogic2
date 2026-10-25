using CodeLogic.Core.Events;

namespace CL.WebLogic.Realtime.Events;

/// <summary>A realtime connection was refused (limits, origin, ticket, anonymous access).</summary>
public sealed record RealtimeConnectionRejectedEvent(string Reason, string Transport) : IEvent;

using CodeLogic.Core.Events;

namespace CL.WebLogic.Auth.Events;

/// <summary>One session of <paramref name="UserId"/> was ended for them: a device was signed out or the oldest session was evicted (<c>Session.MaxPerUser</c>).</summary>
public sealed record SessionEndedEvent(string UserId, string IdHash) : IEvent;

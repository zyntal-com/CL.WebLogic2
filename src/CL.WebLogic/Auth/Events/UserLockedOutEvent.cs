using CodeLogic.Core.Events;

namespace CL.WebLogic.Auth.Events;

/// <summary>An account was locked after repeated failures.</summary>
public sealed record UserLockedOutEvent(string UserId, DateTimeOffset Until) : IEvent;

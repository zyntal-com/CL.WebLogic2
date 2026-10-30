using CodeLogic.Core.Events;

namespace CL.WebLogic.Auth.Events;

/// <summary>An account was deleted (by an administrator when <paramref name="ByUserId"/> is set).</summary>
public sealed record UserDeletedEvent(string UserId, string? ByUserId) : IEvent;

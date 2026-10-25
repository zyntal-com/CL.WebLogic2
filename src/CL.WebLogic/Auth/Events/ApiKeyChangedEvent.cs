using CodeLogic.Core.Events;

namespace CL.WebLogic.Auth.Events;

/// <summary>An API key was created or revoked.</summary>
public sealed record ApiKeyChangedEvent(string KeyId, string? UserId, bool Revoked) : IEvent;

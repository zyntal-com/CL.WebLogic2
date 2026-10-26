using CodeLogic.Core.Events;

namespace CL.WebLogic.Auth.Events;

/// <summary>A passkey was added or removed.</summary>
public sealed record PasskeyChangedEvent(string UserId, string CredentialId, bool Added) : IEvent;

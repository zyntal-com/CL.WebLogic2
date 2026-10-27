using CodeLogic.Core.Events;

namespace CL.WebLogic.Auth.Events;

/// <summary>An external identity (<paramref name="Provider"/>) was linked to or unlinked from a user.</summary>
public sealed record ExternalLoginChangedEvent(string UserId, string Provider, bool Linked) : IEvent;

using CodeLogic.Core.Events;

namespace CL.WebLogic.Auth.Events;

/// <summary>The recovery codes of a user were replaced by new ones.</summary>
public sealed record RecoveryCodesRegeneratedEvent(string UserId) : IEvent;

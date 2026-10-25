using CodeLogic.Core.Events;

namespace CL.WebLogic.Auth.Events;

/// <summary>A recovery code was used.</summary>
public sealed record RecoveryCodeRedeemedEvent(string UserId, int Remaining) : IEvent;

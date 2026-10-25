using CodeLogic.Core.Events;

namespace CL.WebLogic.Auth.Events;

/// <summary>Two-factor authentication was turned on or off.</summary>
public sealed record TwoFactorChangedEvent(string UserId, bool Enabled) : IEvent;

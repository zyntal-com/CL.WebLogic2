using CodeLogic.Core.Events;

namespace CL.WebLogic.Auth.Events;

/// <summary>A password was changed (from a reset link when <paramref name="ViaReset"/>).</summary>
public sealed record PasswordChangedEvent(string UserId, bool ViaReset) : IEvent;

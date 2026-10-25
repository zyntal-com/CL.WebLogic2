using CodeLogic.Core.Events;

namespace CL.WebLogic.Auth.Events;

/// <summary>A user signed out (of all sessions when <paramref name="Everywhere"/>).</summary>
public sealed record UserSignedOutEvent(string UserId, bool Everywhere) : IEvent;

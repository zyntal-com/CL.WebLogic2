using CodeLogic.Core.Events;

namespace CL.WebLogic.Auth.Events;

/// <summary>A new account was created.</summary>
public sealed record UserRegisteredEvent(string UserId) : IEvent;

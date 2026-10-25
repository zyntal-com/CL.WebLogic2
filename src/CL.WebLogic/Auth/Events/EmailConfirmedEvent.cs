using CodeLogic.Core.Events;

namespace CL.WebLogic.Auth.Events;

/// <summary>An e-mail address was confirmed (also after an address change).</summary>
public sealed record EmailConfirmedEvent(string UserId) : IEvent;

using CodeLogic.Core.Events;

namespace CL.WebLogic.Auth.Events;

/// <summary>The e-mail address of a user was changed (after the new address was confirmed; <see cref="EmailConfirmedEvent"/> is raised too).</summary>
public sealed record EmailChangedEvent(string UserId) : IEvent;

using CodeLogic.Core.Events;

namespace CL.WebLogic.Routing.Events;

/// <summary>Published after a contributor's registrations were committed.</summary>
public sealed record WebContributorRegisteredEvent(string ContributorId, string Name, string Kind, int RouteCount) : IEvent;

using CodeLogic.Core.Events;

namespace CL.WebLogic.Routing.Events;

/// <summary>Published after a contributor and all of its registrations were removed (e.g. plugin unload).</summary>
public record WebContributorRemovedEvent(string ContributorId) : IEvent;

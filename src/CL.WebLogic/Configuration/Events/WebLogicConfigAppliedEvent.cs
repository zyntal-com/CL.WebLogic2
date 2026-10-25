using CodeLogic.Core.Events;

namespace CL.WebLogic.Configuration.Events;

/// <summary>Published after a live config change was applied to the running toolkit.</summary>
/// <param name="ChangedProperties">Dotted paths of the properties that changed (never their values).</param>
public record WebLogicConfigAppliedEvent(IReadOnlyList<string> ChangedProperties) : IEvent;

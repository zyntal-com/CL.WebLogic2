using CodeLogic.Core.Events;

namespace CL.WebLogic.Configuration.Events;

/// <summary>
/// Published when a saved config change touches fields marked <c>RequiresRestart</c>.
/// The change is stored but only takes full effect after the host restarts.
/// </summary>
/// <param name="ChangedProperties">Dotted paths of the restart-only properties that changed.</param>
public record WebLogicRestartRequiredEvent(IReadOnlyList<string> ChangedProperties) : IEvent;

using CodeLogic.Core.Events;

namespace CL.WebLogic.Security.Events;

/// <summary>A security stage stopped a request. Carries no client address or credentials.</summary>
/// <param name="Stage">Stage name.</param>
/// <param name="Reason">Internal reason.</param>
/// <param name="Path">Request path.</param>
/// <param name="StatusCode">Response status.</param>
public sealed record WebSecurityRejectedEvent(string Stage, string Reason, string Path, int StatusCode) : IEvent;

using CodeLogic.Core.Events;

namespace CL.WebLogic.Auth.Events;

/// <summary>
/// The first step of a sign-in succeeded and the user must now give their second factor
/// (<paramref name="Methods"/>: the methods used so far, e.g. pwd or link).
/// </summary>
public sealed record SecondFactorRequiredEvent(string UserId, IReadOnlyList<string> Methods) : IEvent;

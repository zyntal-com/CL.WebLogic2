using CodeLogic.Core.Events;

namespace CL.WebLogic.Auth.Events;

/// <summary>A user signed in (<paramref name="Methods"/>: pwd, otp, recovery, link, external…).</summary>
public sealed record UserSignedInEvent(string UserId, string Scheme, IReadOnlyList<string> Methods) : IEvent;

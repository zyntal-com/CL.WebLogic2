using CL.WebLogic.Auth.SignIn;
using CodeLogic.Core.Events;

namespace CL.WebLogic.Auth.Events;

/// <summary>A sign-in attempt failed. <paramref name="UserId"/> is set only for existing users; the login typed is never included.</summary>
public sealed record SignInFailedEvent(string? UserId, WebSignInStatus Status) : IEvent;

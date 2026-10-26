using CodeLogic.Core.Events;

namespace CL.WebLogic.Auth.Events;

/// <summary>
/// A passkey's signature counter went backwards: the authenticator may have been cloned. The
/// sign-in was refused; the app may want to disable the passkey or contact the user.
/// </summary>
public sealed record PasskeyCloneSuspectedEvent(string UserId, string CredentialId, long StoredSignCount, long ReceivedSignCount) : IEvent;

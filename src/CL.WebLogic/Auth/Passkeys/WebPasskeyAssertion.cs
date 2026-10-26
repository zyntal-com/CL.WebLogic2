using CL.WebLogic.Auth.Users;

namespace CL.WebLogic.Auth.Passkeys;

/// <summary>A verified passkey assertion.</summary>
/// <param name="User">The passkey's owner (account rules not yet applied).</param>
/// <param name="Passkey">The passkey as it was before this use.</param>
/// <param name="UserVerified">True when the authenticator verified the user (PIN, biometrics).</param>
public sealed record WebPasskeyAssertion(WebUser User, WebPasskey Passkey, bool UserVerified);

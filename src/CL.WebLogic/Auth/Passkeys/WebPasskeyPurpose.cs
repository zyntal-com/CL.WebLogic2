namespace CL.WebLogic.Auth.Passkeys;

/// <summary>What a passkey assertion is for.</summary>
public enum WebPasskeyPurpose
{
    /// <summary>Passwordless sign-in with a discoverable passkey (any user; user verification required).</summary>
    SignIn,

    /// <summary>The second factor after a password (the pending user's passkeys).</summary>
    SecondFactor,

    /// <summary>Step-up for the signed-in user (user verification required).</summary>
    Reauthentication
}

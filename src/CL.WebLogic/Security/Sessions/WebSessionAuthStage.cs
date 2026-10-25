namespace CL.WebLogic.Security.Sessions;

/// <summary>How far a session has authenticated.</summary>
public enum WebSessionAuthStage
{
    /// <summary>No sign-in.</summary>
    Anonymous,

    /// <summary>Password accepted; a second factor is still required. Not authenticated yet.</summary>
    PendingSecondFactor,

    /// <summary>Fully signed in.</summary>
    Authenticated
}

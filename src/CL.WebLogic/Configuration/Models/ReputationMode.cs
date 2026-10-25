namespace CL.WebLogic.Configuration.Models;

/// <summary>How reputation lookups affect a request.</summary>
public enum ReputationMode
{
    /// <summary>Look up in the background; a listed address is refused from its next request.</summary>
    Background,

    /// <summary>Wait for the lookup (bounded by the timeout) before continuing.</summary>
    Blocking
}

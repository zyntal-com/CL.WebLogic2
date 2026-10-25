namespace CL.WebLogic.Security.Pipeline;

/// <summary>When a security stage runs.</summary>
public enum WebSecurityPhase
{
    /// <summary>Before routing: network, host, transport, throttling and authentication.</summary>
    PreRouting,

    /// <summary>After routing (the endpoint is known): limits, route throttling, authorization, CSRF.</summary>
    Endpoint
}

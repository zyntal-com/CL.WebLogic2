using CL.WebLogic.Results;

namespace CL.WebLogic.Security.Pipeline;

/// <summary>The outcome of a security stage.</summary>
public sealed class WebSecurityDecision
{
    private WebSecurityDecision(WebResult? result, string? reason, int statusCode, string? ruleId = null)
    {
        Result = result;
        Reason = reason;
        StatusCode = statusCode;
        RuleId = ruleId;
    }

    /// <summary>Let the request continue.</summary>
    public static WebSecurityDecision Continue { get; } = new(null, null, 0);

    /// <summary>Stop the request and write <paramref name="result"/>. <paramref name="reason"/> is logged and audited, never sent.</summary>
    public static WebSecurityDecision ShortCircuit(WebResult result, string reason)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new WebSecurityDecision(result, reason, result.StatusCode);
    }

    /// <summary>Stop the request with an error status: ProblemDetails for API clients, the (themed) error page otherwise.</summary>
    public static WebSecurityDecision Reject(int statusCode, string reason) =>
        new(null, reason, statusCode);

    /// <summary>
    /// Like <see cref="Reject(int, string)"/>, naming the rule that matched (reported in
    /// <c>WebSecurityRejectedEvent.RuleId</c> for hit counters).
    /// </summary>
    public static WebSecurityDecision Reject(int statusCode, string reason, string? ruleId) =>
        new(null, reason, statusCode, ruleId);

    /// <summary>True when the request continues.</summary>
    public bool IsContinue => StatusCode == 0;

    /// <summary>The response to write, or null for a plain status rejection.</summary>
    public WebResult? Result { get; }

    /// <summary>Internal reason (for logs and audit).</summary>
    public string? Reason { get; }

    /// <summary>The id of the rule that caused the rejection, when a rule did.</summary>
    public string? RuleId { get; }

    /// <summary>Status code of the rejection.</summary>
    public int StatusCode { get; }
}

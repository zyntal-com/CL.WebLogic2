namespace CL.WebLogic.Security.Pipeline;

/// <summary>
/// One step of the request-security pipeline. Register implementations in DI; they run in
/// <see cref="Order"/> within their <see cref="Phase"/> and can be switched off by
/// <see cref="Name"/> in <c>Security.DisabledStages</c>.
/// </summary>
public interface IWebSecurityStage
{
    /// <summary>Unique, stable name (kebab-case), e.g. <c>authorization</c>.</summary>
    string Name { get; }

    /// <summary>Phase the stage runs in.</summary>
    WebSecurityPhase Phase { get; }

    /// <summary>Position within the phase (lower runs first).</summary>
    int Order { get; }

    /// <summary>Inspects the request; returns <see cref="WebSecurityDecision.Continue"/> or a short-circuit response.</summary>
    ValueTask<WebSecurityDecision> ExecuteAsync(WebSecurityContext context, CancellationToken cancellationToken);
}

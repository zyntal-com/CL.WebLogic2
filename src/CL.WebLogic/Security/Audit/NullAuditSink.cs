namespace CL.WebLogic.Security.Audit;

/// <summary>Default sink: discards entries (the app opts into persistence by registering a sink).</summary>
public sealed class NullAuditSink : IWebAuditSink
{
    /// <inheritdoc/>
    public ValueTask WriteAsync(IReadOnlyList<WebAuditEntry> entries, CancellationToken cancellationToken) => ValueTask.CompletedTask;
}

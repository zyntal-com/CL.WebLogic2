namespace CL.WebLogic.Security.Audit;

/// <summary>
/// Stores audit entries — the app decides where (database, file, SIEM). Receives batches from a
/// background queue, so a slow sink never delays requests.
/// </summary>
public interface IWebAuditSink
{
    /// <summary>Writes a batch.</summary>
    ValueTask WriteAsync(IReadOnlyList<WebAuditEntry> entries, CancellationToken cancellationToken);
}

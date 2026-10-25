using CL.WebLogic.Security.Pipeline;

namespace CL.WebLogic.Security.Audit;

/// <summary>Queues audit entries for the <see cref="IWebAuditSink"/>. Never blocks.</summary>
public interface IWebAuditLog
{
    /// <summary>Entries dropped because the queue was full.</summary>
    long Dropped { get; }

    /// <summary>Queues <paramref name="entry"/>.</summary>
    void Record(WebAuditEntry entry);

    /// <summary>Queues an entry for the current request.</summary>
    void Record(WebSecurityContext security, string category, string outcome, int statusCode = 0, IReadOnlyDictionary<string, string>? data = null);
}

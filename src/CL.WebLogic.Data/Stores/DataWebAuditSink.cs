using CL.WebLogic.Data.Entities;
using CL.WebLogic.Data.Services;
using CL.WebLogic.Security.Audit;

namespace CL.WebLogic.Data.Stores;

/// <summary>
/// Audit records in <c>wl_audit</c>, written in batches by the toolkit's background audit writer
/// (requests never wait for it). Old rows are removed by the sweep (<c>AuditRetention</c>).
/// </summary>
public sealed class DataWebAuditSink : IWebAuditSink
{
    private readonly WebDataDatabase _db;

    /// <summary>Creates the sink.</summary>
    public DataWebAuditSink(WebDataDatabase db) => _db = db ?? throw new ArgumentNullException(nameof(db));

    /// <inheritdoc/>
    public async ValueTask WriteAsync(IReadOnlyList<WebAuditEntry> entries, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Count == 0)
            return;
        WebDataDatabase.Value(await _db.Repository<AuditRow>().InsertManyAsync(entries.Select(ToRow), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Deletes records older than <paramref name="cutoff"/> (called by the sweep).</summary>
    public async Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        var before = cutoff.UtcDateTime;
        return WebDataDatabase.Value(await _db.Query<AuditRow>().Where(a => a.AtUtc < before).DeleteAsync(ct: cancellationToken).ConfigureAwait(false));
    }

    private static AuditRow ToRow(WebAuditEntry entry) => new()
    {
        Id = Guid.CreateVersion7(entry.Timestamp).ToString("D"),
        AtUtc = entry.Timestamp.UtcDateTime,
        Category = Truncate(entry.Category, 100),
        Outcome = Truncate(entry.Outcome, 200),
        StatusCode = entry.StatusCode,
        UserId = entry.UserId,
        TenantId = entry.TenantId,
        IpHash = entry.IpHash,
        Method = Truncate(entry.Method, 16),
        Path = Truncate(entry.Path, 1024),
        TraceId = Truncate(entry.TraceId, 128),
        DataJson = WebDataJson.Write(entry.Data)
    };

    private static string Truncate(string? value, int max) =>
        value is null ? string.Empty : value.Length <= max ? value : value[..max];
}

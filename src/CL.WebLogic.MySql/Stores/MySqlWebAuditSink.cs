using CL.MySQL2.Core;
using CL.WebLogic.MySql.Entities;
using CL.WebLogic.MySql.Services;
using CL.WebLogic.Security.Audit;

namespace CL.WebLogic.MySql.Stores;

/// <summary>
/// Audit records in <c>wl_audit</c>, written in batches by the toolkit's background audit writer
/// (requests never wait for it). Old rows are removed by the sweep (<c>AuditRetention</c>).
/// </summary>
public sealed class MySqlWebAuditSink : IWebAuditSink
{
    private readonly WebMySqlDatabase _db;

    /// <summary>Creates the sink.</summary>
    public MySqlWebAuditSink(WebMySqlDatabase db) => _db = db ?? throw new ArgumentNullException(nameof(db));

    /// <inheritdoc/>
    public async ValueTask WriteAsync(IReadOnlyList<WebAuditEntry> entries, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Count == 0)
            return;
        WebMySqlDatabase.Value(await _db.Repository<AuditRow>().InsertManyAsync(entries.Select(ToRow), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Deletes records older than <paramref name="cutoff"/> (called by the sweep).</summary>
    public async Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        var before = cutoff.UtcDateTime;
        return WebMySqlDatabase.Value(await _db.Query<AuditRow>().Where(a => a.AtUtc < before).DeleteAsync(cancellationToken).ConfigureAwait(false));
    }

    private static AuditRow ToRow(WebAuditEntry entry) => new()
    {
        Id = SequentialGuid.NewId(entry.Timestamp).ToString("D"),
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
        DataJson = WebMySqlJson.Write(entry.Data)
    };

    private static string Truncate(string? value, int max) =>
        value is null ? string.Empty : value.Length <= max ? value : value[..max];
}

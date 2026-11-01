using CL.Data;

namespace CL.WebLogic.Data.Entities;

/// <summary>One audit record (client addresses are HMAC hashes).</summary>
[Table(Name = "wl_audit")]
[MySqlTable(Collation = WebDataCollations.MySql)]
public sealed class AuditRow
{
    /// <summary>Sequential id.</summary>
    [Column(Name = "id", DataType = DataType.FixedString, Size = 36, Primary = true, NotNull = true)]
    [SqlServerColumn(Collation = WebDataCollations.SqlServer)]
    public string Id { get; set; } = string.Empty;

    /// <summary>When (UTC).</summary>
    [Column(Name = "at_utc", DataType = DataType.DateTime, NotNull = true, Index = true)]
    public DateTime AtUtc { get; set; }

    /// <summary>Category, e.g. auth.signin.</summary>
    [Column(Name = "category", DataType = DataType.String, Size = 100, NotNull = true, Index = true)]
    [SqlServerColumn(Collation = WebDataCollations.SqlServer)]
    public string Category { get; set; } = string.Empty;

    /// <summary>Outcome.</summary>
    [Column(Name = "outcome", DataType = DataType.String, Size = 200, NotNull = true)]
    public string Outcome { get; set; } = string.Empty;

    /// <summary>Response status.</summary>
    [Column(Name = "status_code", DataType = DataType.Int, NotNull = true)]
    public int StatusCode { get; set; }

    /// <summary>User, if known.</summary>
    [Column(Name = "user_id", DataType = DataType.String, Size = 64, Index = true)]
    [SqlServerColumn(Collation = WebDataCollations.SqlServer)]
    public string? UserId { get; set; }

    /// <summary>Tenant, if any.</summary>
    [Column(Name = "tenant_id", DataType = DataType.String, Size = 128)]
    [SqlServerColumn(Collation = WebDataCollations.SqlServer)]
    public string? TenantId { get; set; }

    /// <summary>HMAC of the client address.</summary>
    [Column(Name = "ip_hash", DataType = DataType.String, Size = 128)]
    public string? IpHash { get; set; }

    /// <summary>HTTP method.</summary>
    [Column(Name = "method", DataType = DataType.String, Size = 16, NotNull = true)]
    public string Method { get; set; } = string.Empty;

    /// <summary>Request path.</summary>
    [Column(Name = "path", DataType = DataType.String, Size = 1024, NotNull = true)]
    public string Path { get; set; } = string.Empty;

    /// <summary>Trace id.</summary>
    [Column(Name = "trace_id", DataType = DataType.String, Size = 128, NotNull = true)]
    public string TraceId { get; set; } = string.Empty;

    /// <summary>Extra values as JSON.</summary>
    [Column(Name = "data_json", DataType = DataType.Text, NotNull = true)]
    public string DataJson { get; set; } = "{}";
}

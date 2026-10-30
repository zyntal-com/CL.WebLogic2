using System.Threading.Channels;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Security.Pipeline;

namespace CL.WebLogic.Security.Audit;

/// <summary>Bounded in-memory queue in front of the sink. When full, the oldest entries are dropped and counted.</summary>
public sealed class WebAuditLog : IWebAuditLog
{
    private readonly Channel<WebAuditEntry> _channel;
    private readonly WebLogicConfigHolder _config;
    private readonly IWebIpHasher _ipHasher;
    private readonly TimeProvider _time;
    private long _dropped;

    /// <summary>Creates the queue (capacity from <c>Security.Audit.QueueCapacity</c>, fixed at startup).</summary>
    public WebAuditLog(WebLogicConfigHolder config, IWebIpHasher ipHasher, TimeProvider? time = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _ipHasher = ipHasher ?? throw new ArgumentNullException(nameof(ipHasher));
        _time = time ?? TimeProvider.System;
        var options = new BoundedChannelOptions(Math.Max(100, config.Current.Security.Audit.QueueCapacity))
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true
        };
        _channel = Channel.CreateBounded<WebAuditEntry>(options, _ => Interlocked.Increment(ref _dropped));
    }

    /// <inheritdoc/>
    public long Dropped => Interlocked.Read(ref _dropped);

    /// <summary>The consumer side (read by <see cref="AuditFlushService"/>).</summary>
    internal ChannelReader<WebAuditEntry> Reader => _channel.Reader;

    /// <summary>Entries waiting for the sink (for the queue-length metric).</summary>
    internal int QueueLength => _channel.Reader.Count;

    /// <inheritdoc/>
    public void Record(WebAuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (_config.Current.Security.Audit.Enabled)
            _channel.Writer.TryWrite(entry);
    }

    /// <inheritdoc/>
    public void Record(WebSecurityContext security, string category, string outcome, int statusCode = 0, IReadOnlyDictionary<string, string>? data = null)
    {
        ArgumentNullException.ThrowIfNull(security);
        if (!_config.Current.Security.Audit.Enabled)
            return;

        var request = security.HttpContext.Request;
        Record(new WebAuditEntry
        {
            Timestamp = _time.GetUtcNow(),
            Category = category,
            Outcome = outcome,
            StatusCode = statusCode,
            UserId = security.Identity.UserId,
            TenantId = security.TenantId,
            IpHash = _ipHasher.Hash(security.ClientIp),
            Method = request.Method,
            Path = request.Path.Value ?? "/",
            TraceId = security.HttpContext.TraceIdentifier,
            Data = data ?? new Dictionary<string, string>()
        });
    }
}

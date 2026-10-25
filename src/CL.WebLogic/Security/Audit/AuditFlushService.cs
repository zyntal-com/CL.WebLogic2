using CL.WebLogic.Hosting.Services;
using Microsoft.Extensions.Hosting;

namespace CL.WebLogic.Security.Audit;

/// <summary>Moves queued audit entries to the sink in batches.</summary>
public sealed class AuditFlushService : BackgroundService
{
    private const int BatchSize = 256;

    private readonly WebAuditLog _queue;
    private readonly IWebAuditSink _sink;
    private readonly IWebLogicContext _context;

    /// <summary>Creates the service.</summary>
    public AuditFlushService(WebAuditLog queue, IWebAuditSink sink, IWebLogicContext context)
    {
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new List<WebAuditEntry>(BatchSize);
        try
        {
            while (await _queue.Reader.WaitToReadAsync(stoppingToken).ConfigureAwait(false))
            {
                while (batch.Count < BatchSize && _queue.Reader.TryRead(out var entry))
                    batch.Add(entry);
                await FlushAsync(batch, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down: flush what is left without the cancelled token.
            while (_queue.Reader.TryRead(out var entry))
                batch.Add(entry);
            await FlushAsync(batch, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task FlushAsync(List<WebAuditEntry> batch, CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
            return;
        try
        {
            await _sink.WriteAsync([.. batch], cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _context.Logger.Error($"Audit sink failed; {batch.Count} entries lost: {ex.Message}", ex);
        }
        batch.Clear();
    }
}

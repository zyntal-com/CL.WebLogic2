using Microsoft.AspNetCore.Http.Features;

namespace CL.WebLogic.Caching.Services;

/// <summary>
/// Response body used while an ETag is computed: keeps the body in memory up to a limit, then
/// writes what it has to the real body and passes everything after it straight through (no ETag).
/// Flushes are ignored while buffering, so serializers that flush at the end do not end the buffer.
/// </summary>
internal sealed class WebETagBufferStream : Stream
{
    private readonly IHttpResponseBodyFeature _target;
    private readonly int _limit;
    private MemoryStream? _buffer = new();

    /// <summary>Creates the stream over the real response body.</summary>
    public WebETagBufferStream(IHttpResponseBodyFeature target, int limit)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _limit = limit;
    }

    /// <summary>True once the body outgrew the limit and went to the real body.</summary>
    public bool PassedThrough => _buffer is null;

    /// <summary>The buffered body (empty once passed through).</summary>
    public ReadOnlyMemory<byte> Buffered => _buffer is null ? ReadOnlyMemory<byte>.Empty : _buffer.GetBuffer().AsMemory(0, (int)_buffer.Length);

    /// <inheritdoc/>
    public override bool CanRead => false;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => true;

    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    /// <inheritdoc/>
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (_buffer is not null && _buffer.Length + buffer.Length <= _limit)
        {
            _buffer.Write(buffer);
            return;
        }
        if (_buffer is not null)
        {
            _target.Stream.Write(Buffered.Span);
            _buffer.Dispose();
            _buffer = null;
        }
        _target.Stream.Write(buffer);
    }

    /// <inheritdoc/>
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc/>
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_buffer is not null && _buffer.Length + buffer.Length <= _limit)
        {
            _buffer.Write(buffer.Span);
            return;
        }
        if (_buffer is not null)
        {
            await _target.Stream.WriteAsync(Buffered, cancellationToken).ConfigureAwait(false);
            await _buffer.DisposeAsync().ConfigureAwait(false);
            _buffer = null;
        }
        await _target.Stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override void Flush()
    {
        if (_buffer is null)
            _target.Stream.Flush();
    }

    /// <inheritdoc/>
    public override Task FlushAsync(CancellationToken cancellationToken) =>
        _buffer is null ? _target.Stream.FlushAsync(cancellationToken) : Task.CompletedTask;

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _buffer?.Dispose();
            _buffer = null;
        }
        base.Dispose(disposing);
    }
}

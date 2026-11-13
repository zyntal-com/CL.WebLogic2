using System.IO.Pipelines;
using System.Text;
using Microsoft.Extensions.ObjectPool;

namespace CL.WebLogic.Templates.Rendering;

/// <summary>
/// A rendered page that is still in the renderer's pooled buffer, so it can be written to the response without first
/// being copied into one large string. Dispose it once it has been written (or discarded) to give the buffer back.
/// </summary>
public sealed class RenderedTemplate : IDisposable
{
    private readonly ObjectPool<StringBuilder>? _pool;
    private StringBuilder? _buffer;
    private string? _text;

    internal RenderedTemplate(StringBuilder buffer, ObjectPool<StringBuilder> pool)
    {
        _buffer = buffer;
        _pool = pool;
    }

    private RenderedTemplate(string text) => _text = text;

    /// <summary>Wraps text that was rendered as a string (renderers that do not buffer).</summary>
    public static RenderedTemplate FromText(string text) => new(text ?? throw new ArgumentNullException(nameof(text)));

    /// <summary>The number of characters.</summary>
    public int Length => _buffer?.Length ?? _text?.Length ?? 0;

    /// <summary>Writes the page to <paramref name="writer"/> as UTF-8 and flushes it.</summary>
    public async ValueTask WriteToAsync(PipeWriter writer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ObjectDisposedException.ThrowIf(_buffer is null && _text is null, this);

        if (_text is not null)
        {
            Encode(writer, _text, flush: true, new UTF8Encoding(false).GetEncoder());
        }
        else
        {
            // One stateful encoder across the chunks: a surrogate pair may be split between two of them.
            var encoder = new UTF8Encoding(false).GetEncoder();
            foreach (var chunk in _buffer!.GetChunks())
                Encode(writer, chunk.Span, flush: false, encoder);
            Encode(writer, [], flush: true, encoder);
        }
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override string ToString() => _text ?? _buffer?.ToString() ?? string.Empty;

    /// <summary>Gives the buffer back to the pool. The page cannot be written afterwards.</summary>
    public void Dispose()
    {
        if (_buffer is { } buffer)
            _pool!.Return(buffer);
        _buffer = null;
        _text = null;
    }

    private static void Encode(PipeWriter writer, ReadOnlySpan<char> chars, bool flush, Encoder encoder)
    {
        while (true)
        {
            if (chars.IsEmpty && !flush)
                return;
            var span = writer.GetSpan(4096);
            encoder.Convert(chars, span, flush, out var charsUsed, out var bytesUsed, out var completed);
            writer.Advance(bytesUsed);
            chars = chars[charsUsed..];
            if (chars.IsEmpty && completed)
                return;
        }
    }
}

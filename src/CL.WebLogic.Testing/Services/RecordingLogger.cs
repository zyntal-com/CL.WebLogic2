using System.Collections.Concurrent;
using CodeLogic.Core.Logging;

namespace CL.WebLogic.Testing.Services;

/// <summary>An <see cref="ILogger"/> that records every message for assertions.</summary>
public sealed class RecordingLogger : ILogger
{
    private readonly ConcurrentQueue<RecordedLogEntry> _entries = new();

    /// <summary>All messages recorded so far, in order.</summary>
    public IReadOnlyList<RecordedLogEntry> Entries => [.. _entries];

    /// <summary>True when any recorded message at <paramref name="level"/> contains <paramref name="fragment"/>.</summary>
    public bool Contains(LogLevel level, string fragment) =>
        _entries.Any(e => e.Level == level && e.Message.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc/>
    public void Trace(string message) => Record(LogLevel.Trace, message, null);

    /// <inheritdoc/>
    public void Debug(string message) => Record(LogLevel.Debug, message, null);

    /// <inheritdoc/>
    public void Info(string message) => Record(LogLevel.Info, message, null);

    /// <inheritdoc/>
    public void Warning(string message) => Record(LogLevel.Warning, message, null);

    /// <inheritdoc/>
    public void Error(string message, Exception? exception = null) => Record(LogLevel.Error, message, exception);

    /// <inheritdoc/>
    public void Critical(string message, Exception? exception = null) => Record(LogLevel.Critical, message, exception);

    private void Record(LogLevel level, string message, Exception? exception) =>
        _entries.Enqueue(new RecordedLogEntry(level, message, exception));
}

/// <summary>One message captured by <see cref="RecordingLogger"/>.</summary>
public sealed record RecordedLogEntry(LogLevel Level, string Message, Exception? Exception);

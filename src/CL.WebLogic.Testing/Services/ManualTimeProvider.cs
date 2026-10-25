namespace CL.WebLogic.Testing.Services;

/// <summary>
/// A <see cref="TimeProvider"/> whose clock only moves when told to — for testing session
/// timeouts, rate-limit refills and cache expiry. Register it in place of
/// <see cref="TimeProvider.System"/> (<c>services.AddSingleton&lt;TimeProvider&gt;(clock)</c>).
/// </summary>
public sealed class ManualTimeProvider : TimeProvider
{
    private long _utcTicks;

    /// <summary>Creates the clock at <paramref name="start"/> (default 2030-01-01 UTC).</summary>
    public ManualTimeProvider(DateTimeOffset? start = null) =>
        _utcTicks = (start ?? new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero)).UtcTicks;

    /// <inheritdoc/>
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref _utcTicks), TimeSpan.Zero);

    /// <inheritdoc/>
    public override long GetTimestamp() => Interlocked.Read(ref _utcTicks);

    /// <summary>Moves the clock forward.</summary>
    public void Advance(TimeSpan by)
    {
        if (by < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(by), "Time only moves forward.");
        Interlocked.Add(ref _utcTicks, by.Ticks);
    }
}

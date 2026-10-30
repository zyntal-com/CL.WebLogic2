using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using CL.WebLogic.Diagnostics.Models;
using CL.WebLogic.Diagnostics.Services;
using CL.WebLogic.Testing.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CL.WebLogic.IntegrationTests.Diagnostics;

/// <summary>
/// Listens to one host's WebLogic meter (other test hosts in the process are ignored) and to
/// WebLogic spans, the way OpenTelemetry would.
/// </summary>
internal sealed class MetricsCapture : IDisposable
{
    private readonly MeterListener _meters = new();
    private readonly ActivityListener _spans;
    private readonly ConcurrentQueue<Measurement> _measurements = new();
    private readonly ConcurrentQueue<Activity> _activities = new();

    public MetricsCapture(WebLogicTestHost host)
    {
        var meter = host.Services.GetRequiredService<WebLogicMetrics>().Meter;
        _meters.InstrumentPublished = (instrument, listener) =>
        {
            if (ReferenceEquals(instrument.Meter, meter))
                listener.EnableMeasurementEvents(instrument);
        };
        _meters.SetMeasurementEventCallback<long>((i, value, tags, _) => Add(i, value, tags));
        _meters.SetMeasurementEventCallback<int>((i, value, tags, _) => Add(i, value, tags));
        _meters.SetMeasurementEventCallback<double>((i, value, tags, _) => Add(i, value, tags));
        _meters.Start();

        _spans = new ActivityListener
        {
            ShouldListenTo = source => source.Name is WebLogicInstruments.ActivitySourceName or "Microsoft.AspNetCore",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => _activities.Enqueue(activity)
        };
        ActivitySource.AddActivityListener(_spans);
    }

    /// <summary>Every measurement so far.</summary>
    public IReadOnlyList<Measurement> Measurements => [.. _measurements];

    /// <summary>Every stopped span so far.</summary>
    public IReadOnlyList<Activity> Activities => [.. _activities];

    /// <summary>Measurements of <paramref name="instrument"/>.</summary>
    public IReadOnlyList<Measurement> Of(string instrument) => [.. _measurements.Where(m => m.Instrument == instrument)];

    /// <summary>Reads the observable instruments now.</summary>
    public void Observe() => _meters.RecordObservableInstruments();

    public void Dispose()
    {
        _meters.Dispose();
        _spans.Dispose();
    }

    private void Add(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var copy = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var tag in tags)
            copy[tag.Key] = Convert.ToString(tag.Value, System.Globalization.CultureInfo.InvariantCulture);
        _measurements.Enqueue(new Measurement(instrument.Name, value, copy));
    }

    /// <summary>One recorded value with its tags.</summary>
    public sealed record Measurement(string Instrument, double Value, IReadOnlyDictionary<string, string?> Tags)
    {
        public string? this[string tag] => Tags.TryGetValue(tag, out var value) ? value : null;

        public override string ToString() => Instrument + " " + string.Join(", ", Tags.Select(t => t.Key + "=" + t.Value));
    }
}

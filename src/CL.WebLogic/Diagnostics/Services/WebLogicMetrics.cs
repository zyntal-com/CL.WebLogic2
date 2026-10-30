using System.Diagnostics;
using System.Diagnostics.Metrics;
using CL.WebLogic.Diagnostics.Models;
using CL.WebLogic.Security.Pipeline;

namespace CL.WebLogic.Diagnostics.Services;

/// <summary>
/// WebLogic's meter, activity source and instruments (names in <see cref="WebLogicInstruments"/>).
/// Recording costs a flag check when nothing listens: instruments are only measured when a
/// <see cref="MeterListener"/> (OpenTelemetry, <c>dotnet-counters</c>) enabled them, and spans
/// are only created when an <see cref="ActivityListener"/> samples the source.
/// </summary>
public sealed class WebLogicMetrics : IDisposable
{
    private static readonly double[] DurationBuckets = [0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10];

    private readonly Meter _meter;
    private readonly bool _ownsMeter;
    private readonly ActivitySource _source;
    private readonly Counter<long> _requests;
    private readonly Histogram<double> _duration;
    private readonly Counter<long> _rejections;
    private readonly Counter<long> _rateLimited;
    private readonly Counter<long> _signIns;
    private readonly Lock _lock = new();
    private bool _observing;

    /// <summary>Creates the instruments, on a meter from <paramref name="meterFactory"/> when the host provides one.</summary>
    public WebLogicMetrics(IMeterFactory? meterFactory = null)
    {
        var version = typeof(WebLogicMetrics).Assembly.GetName().Version?.ToString();
        if (meterFactory is not null)
        {
            _meter = meterFactory.Create(new MeterOptions(WebLogicInstruments.MeterName) { Version = version });
        }
        else
        {
            _meter = new Meter(WebLogicInstruments.MeterName, version);
            _ownsMeter = true;
        }
        _source = new ActivitySource(WebLogicInstruments.ActivitySourceName, version);

        _requests = _meter.CreateCounter<long>(WebLogicInstruments.RouteRequests, "{request}", "Requests handled by WebLogic routes.");
        _duration = _meter.CreateHistogram(WebLogicInstruments.RouteDuration, "s", "Time spent in WebLogic route middleware, handler and result.",
            tags: null, advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = DurationBuckets });
        _rejections = _meter.CreateCounter<long>(WebLogicInstruments.SecurityRejections, "{request}", "Requests stopped by a security stage.");
        _rateLimited = _meter.CreateCounter<long>(WebLogicInstruments.RateLimitRejections, "{request}", "Requests or attempts refused by a rate limit.");
        _signIns = _meter.CreateCounter<long>(WebLogicInstruments.SignIns, "{sign_in}", "Sign-in outcomes.");
    }

    /// <summary>The meter (for tests and listeners that filter by instance).</summary>
    public Meter Meter => _meter;

    /// <summary>True when a listener measures the route instruments (skip the timing otherwise).</summary>
    public bool RouteMetricsEnabled => _requests.Enabled || _duration.Enabled;

    /// <summary>Records one handled route request.</summary>
    public void RecordRequest(string routePattern, string method, int statusCode, TimeSpan elapsed)
    {
        var tags = new TagList
        {
            { WebLogicInstruments.RouteTag, routePattern },
            { WebLogicInstruments.MethodTag, WebMetricTags.Method(method) },
            { WebLogicInstruments.StatusClassTag, WebMetricTags.StatusClass(statusCode) }
        };
        _requests.Add(1, tags);
        _duration.Record(elapsed.TotalSeconds, tags);
    }

    /// <summary>Records a security-stage rejection (the reason keeps only its code).</summary>
    public void RecordRejection(string stage, string? reason, int statusCode)
    {
        if (!_rejections.Enabled)
            return;
        _rejections.Add(1, new TagList
        {
            { WebLogicInstruments.StageTag, WebMetricTags.Name(stage) },
            { WebLogicInstruments.ReasonTag, WebMetricTags.ReasonCode(reason) },
            { WebLogicInstruments.StatusCodeTag, statusCode }
        });
    }

    /// <summary>Records a rate-limit refusal; <paramref name="scope"/> is <c>global</c>, <c>route</c>, <c>realtime</c> or <c>sign_in</c>.</summary>
    public void RecordRateLimited(string scope, string? reason = null)
    {
        if (!_rateLimited.Enabled)
            return;
        var tags = new TagList { { WebLogicInstruments.RateLimitScopeTag, scope } };
        if (reason is not null)
            tags.Add(WebLogicInstruments.ReasonTag, WebMetricTags.ReasonCode(reason));
        _rateLimited.Add(1, tags);
    }

    /// <summary>Records a sign-in outcome; <paramref name="methods"/> and <paramref name="reason"/> are optional.</summary>
    public void RecordSignIn(string outcome, IReadOnlyList<string>? methods, string? reason = null)
    {
        if (!_signIns.Enabled)
            return;
        var tags = new TagList { { WebLogicInstruments.SignInOutcomeTag, outcome } };
        if (methods is not null)
            tags.Add(WebLogicInstruments.SignInMethodTag, WebMetricTags.Methods(methods));
        if (reason is not null)
            tags.Add(WebLogicInstruments.SignInReasonTag, reason);
        _signIns.Add(1, tags);
    }

    /// <summary>Starts the span for a security phase, or returns null when nothing listens.</summary>
    public Activity? StartSecurityPhase(WebSecurityPhase phase) =>
        _source.HasListeners()
            ? _source.StartActivity(phase == WebSecurityPhase.PreRouting ? WebLogicInstruments.SecurityPreRoutingSpan : WebLogicInstruments.SecurityEndpointSpan)
            : null;

    /// <summary>Marks a security span as rejected by <paramref name="stage"/>.</summary>
    public static void MarkRejected(Activity? activity, string stage, string? reason, int statusCode)
    {
        if (activity is null)
            return;
        activity.SetTag(WebLogicInstruments.SecurityOutcomeTag, "rejected");
        activity.SetTag(WebLogicInstruments.StageTag, WebMetricTags.Name(stage));
        activity.SetTag(WebLogicInstruments.ReasonTag, WebMetricTags.ReasonCode(reason));
        activity.SetTag(WebLogicInstruments.StatusCodeTag, statusCode);
    }

    /// <summary>Starts the span for a template render, or returns null when nothing listens.</summary>
    public Activity? StartTemplateRender(string templatePath)
    {
        if (!_source.HasListeners())
            return null;
        var activity = _source.StartActivity(WebLogicInstruments.TemplateRenderSpan);
        activity?.SetTag(WebLogicInstruments.TemplateTag, WebMetricTags.Name(templatePath));
        return activity;
    }

    /// <summary>
    /// Adds the observable instruments (connections, queue lengths). Called once by
    /// <see cref="WebLogicMetricsRecorder"/>; later calls are ignored.
    /// </summary>
    internal void Observe(Func<int> realtimeConnections, Func<int>? notificationQueue, Func<int>? auditQueue, Func<long> auditDropped)
    {
        lock (_lock)
        {
            if (_observing)
                return;
            _observing = true;
        }
        _meter.CreateObservableUpDownCounter(WebLogicInstruments.RealtimeConnections, realtimeConnections, "{connection}", "Open realtime connections on this node.");
        if (notificationQueue is not null)
            _meter.CreateObservableUpDownCounter(WebLogicInstruments.NotificationQueueLength, notificationQueue, "{notification}", "Security e-mails waiting to be sent.");
        if (auditQueue is not null)
            _meter.CreateObservableUpDownCounter(WebLogicInstruments.AuditQueueLength, auditQueue, "{entry}", "Audit entries waiting for the sink.");
        _meter.CreateObservableCounter(WebLogicInstruments.AuditDropped, auditDropped, "{entry}", "Audit entries dropped because the queue was full.");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _source.Dispose();
        if (_ownsMeter)
            _meter.Dispose();
    }
}

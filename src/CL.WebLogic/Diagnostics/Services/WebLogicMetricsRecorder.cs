using CL.WebLogic.Auth.Events;
using CL.WebLogic.Auth.Notifications;
using CL.WebLogic.Auth.SignIn;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Realtime.Events;
using CL.WebLogic.Realtime.Services;
using CL.WebLogic.Security.Audit;
using CL.WebLogic.Security.Events;
using CodeLogic.Core.Events;
using Microsoft.Extensions.Hosting;

namespace CL.WebLogic.Diagnostics.Services;

/// <summary>
/// Feeds <see cref="WebLogicMetrics"/> from the events WebLogic already publishes (security
/// rejections, sign-in outcomes, refused realtime connections) and registers the observable
/// instruments (open realtime connections, notification and audit queue lengths). Only codes
/// and outcomes are recorded; user ids, paths and addresses in the events are ignored.
/// </summary>
public sealed class WebLogicMetricsRecorder : IHostedService
{
    private static readonly HashSet<string> RealtimeLimitReasons = new(["max-connections", "max-connections-per-client"], StringComparer.Ordinal);

    private readonly WebLogicMetrics _metrics;
    private readonly IWebLogicContext _context;
    private readonly WebRealtimeHub _realtime;
    private readonly WebNotificationDispatcher _notifications;
    private readonly IWebAuditLog _audit;
    private readonly List<IDisposable> _subscriptions = [];

    /// <summary>Creates the recorder.</summary>
    public WebLogicMetricsRecorder(WebLogicMetrics metrics, IWebLogicContext context, WebRealtimeHub realtime, WebNotificationDispatcher notifications, IWebAuditLog audit)
    {
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _realtime = realtime ?? throw new ArgumentNullException(nameof(realtime));
        _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var auditLog = _audit as WebAuditLog;
        var realtime = _realtime;
        var notifications = _notifications;
        var audit = _audit;
        _metrics.Observe(
            () => realtime.ConnectionCount,
            () => notifications.QueueLength,
            auditLog is null ? null : () => auditLog.QueueLength,
            () => audit.Dropped);

        On<WebSecurityRejectedEvent>(OnRejected);
        On<UserSignedInEvent>(e =>
        {
            if (!e.IsRefresh)
                _metrics.RecordSignIn("succeeded", e.Methods);
        });
        On<SecondFactorRequiredEvent>(e => _metrics.RecordSignIn("two_factor_required", e.Methods));
        On<SignInFailedEvent>(OnSignInFailed);
        On<UserLockedOutEvent>(_ => _metrics.RecordSignIn("locked", null));
        On<RealtimeConnectionRejectedEvent>(e =>
        {
            if (RealtimeLimitReasons.Contains(e.Reason))
                _metrics.RecordRateLimited("realtime", e.Reason);
        });
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var subscription in _subscriptions)
            subscription.Dispose();
        _subscriptions.Clear();
        return Task.CompletedTask;
    }

    private void OnRejected(WebSecurityRejectedEvent e)
    {
        _metrics.RecordRejection(e.Stage, e.Reason, e.StatusCode);
        if (e.StatusCode == 429)
        {
            var scope = e.Stage switch
            {
                "global-rate-limit" => "global",
                "route-rate-limit" => "route",
                _ => WebMetricTags.Name(e.Stage) // an app stage that throttles
            };
            _metrics.RecordRateLimited(scope, e.Reason);
        }
    }

    private void OnSignInFailed(SignInFailedEvent e)
    {
        var reason = FailureReason(e.Status);
        _metrics.RecordSignIn("failed", null, reason);
        if (e.Status == WebSignInStatus.Throttled)
            _metrics.RecordRateLimited("sign_in");
    }

    private static string FailureReason(WebSignInStatus status) => status switch
    {
        WebSignInStatus.InvalidCredentials => "invalid_credentials",
        WebSignInStatus.LockedOut => "locked_out",
        WebSignInStatus.NotAllowed => "not_allowed",
        WebSignInStatus.Throttled => "throttled",
        WebSignInStatus.Suspended => "suspended",
        _ => WebMetricTags.Other
    };

    private void On<TEvent>(Action<TEvent> handler) where TEvent : IEvent =>
        _subscriptions.Add(_context.Events.Subscribe(handler));
}

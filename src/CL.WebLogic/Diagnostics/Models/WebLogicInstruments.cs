namespace CL.WebLogic.Diagnostics.Models;

/// <summary>
/// Names of WebLogic's <see cref="System.Diagnostics.Metrics.Meter"/>, its
/// <see cref="System.Diagnostics.ActivitySource"/>, instruments, spans and tags. Hand
/// <see cref="MeterName"/> / <see cref="ActivitySourceName"/> to OpenTelemetry
/// (<c>AddMeter</c>, <c>AddSource</c>). Tag values are always low-cardinality and never carry a
/// client address, user id, query string, token or config value.
/// </summary>
public static class WebLogicInstruments
{
    /// <summary>The meter name.</summary>
    public const string MeterName = "CodeLogic.WebLogic";

    /// <summary>The activity source name.</summary>
    public const string ActivitySourceName = "CodeLogic.WebLogic";

    // ── Instruments ─────────────────────────────────────────────────────────

    /// <summary>Counter: requests handled by WebLogic routes.</summary>
    public const string RouteRequests = "weblogic.route.requests";

    /// <summary>Histogram (seconds): time spent in WebLogic route handlers, middleware and result execution.</summary>
    public const string RouteDuration = "weblogic.route.duration";

    /// <summary>Counter: requests stopped by a security stage.</summary>
    public const string SecurityRejections = "weblogic.security.rejections";

    /// <summary>Counter: requests or attempts refused by a rate limit.</summary>
    public const string RateLimitRejections = "weblogic.rate_limit.rejections";

    /// <summary>Counter: sign-in outcomes.</summary>
    public const string SignIns = "weblogic.auth.sign_ins";

    /// <summary>Observable up-down counter: open realtime connections on this node.</summary>
    public const string RealtimeConnections = "weblogic.realtime.connections";

    /// <summary>Observable up-down counter: security e-mails waiting to be sent.</summary>
    public const string NotificationQueueLength = "weblogic.notifications.queue_length";

    /// <summary>Observable up-down counter: audit entries waiting for the sink.</summary>
    public const string AuditQueueLength = "weblogic.audit.queue_length";

    /// <summary>Observable counter: audit entries dropped because the queue was full.</summary>
    public const string AuditDropped = "weblogic.audit.dropped";

    // ── Spans ───────────────────────────────────────────────────────────────

    /// <summary>Span: the pre-routing security stages.</summary>
    public const string SecurityPreRoutingSpan = "weblogic.security.pre_routing";

    /// <summary>Span: the endpoint security stages.</summary>
    public const string SecurityEndpointSpan = "weblogic.security.endpoint";

    /// <summary>Span: one template render (page, partial page, widget template).</summary>
    public const string TemplateRenderSpan = "weblogic.template.render";

    // ── Tags ────────────────────────────────────────────────────────────────

    /// <summary>Tag: the route pattern (never the raw path).</summary>
    public const string RouteTag = "http.route";

    /// <summary>Tag: the request method (<c>_OTHER</c> for non-standard methods).</summary>
    public const string MethodTag = "http.request.method";

    /// <summary>Tag: the response status code.</summary>
    public const string StatusCodeTag = "http.response.status_code";

    /// <summary>Tag: the response status class (<c>2xx</c>, <c>4xx</c>…).</summary>
    public const string StatusClassTag = "weblogic.status_class";

    /// <summary>Tag: the security stage name.</summary>
    public const string StageTag = "weblogic.security.stage";

    /// <summary>Tag: the reason code without its variable part (<c>country-denied:DK</c> becomes <c>country-denied</c>).</summary>
    public const string ReasonTag = "weblogic.security.reason";

    /// <summary>Tag: <c>passed</c> or <c>rejected</c> (security spans).</summary>
    public const string SecurityOutcomeTag = "weblogic.security.outcome";

    /// <summary>Tag: which limit refused the request: <c>global</c>, <c>route</c>, <c>realtime</c>, <c>sign_in</c>, or an app stage that answered 429.</summary>
    public const string RateLimitScopeTag = "weblogic.rate_limit.scope";

    /// <summary>Tag: <c>succeeded</c>, <c>failed</c>, <c>locked</c> or <c>two_factor_required</c>.</summary>
    public const string SignInOutcomeTag = "weblogic.auth.outcome";

    /// <summary>Tag: the authentication methods used (<c>pwd</c>, <c>pwd,otp</c>, <c>passkey</c>, <c>ext:google</c>…).</summary>
    public const string SignInMethodTag = "weblogic.auth.method";

    /// <summary>Tag: why a sign-in failed (<c>invalid_credentials</c>, <c>throttled</c>…).</summary>
    public const string SignInReasonTag = "weblogic.auth.reason";

    /// <summary>Tag: the normalized template path (server-defined).</summary>
    public const string TemplateTag = "weblogic.template.path";
}

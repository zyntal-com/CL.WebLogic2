# Observability

WebLogic publishes metrics on a `System.Diagnostics.Metrics` meter and spans on an `ActivitySource`,
both named `CodeLogic.WebLogic` (`WebLogicInstruments.MeterName` / `WebLogicInstruments.ActivitySourceName`).
Nothing has to be switched on, and nothing is exported by WebLogic itself: until a listener
(OpenTelemetry, `dotnet-counters`, `dotnet-trace`) attaches, recording is a flag check and no span is
created. WebLogic does not reference OpenTelemetry; the app adds the packages it wants.

## Metrics

| Instrument | Type | Unit | Tags |
|---|---|---|---|
| `weblogic.route.requests` | Counter | `{request}` | `http.route`, `http.request.method`, `weblogic.status_class` |
| `weblogic.route.duration` | Histogram | `s` | same as above |
| `weblogic.security.rejections` | Counter | `{request}` | `weblogic.security.stage`, `weblogic.security.reason`, `http.response.status_code` |
| `weblogic.rate_limit.rejections` | Counter | `{request}` | `weblogic.rate_limit.scope`, `weblogic.security.reason` (when there is one) |
| `weblogic.auth.sign_ins` | Counter | `{sign_in}` | `weblogic.auth.outcome`, `weblogic.auth.method`, `weblogic.auth.reason` |
| `weblogic.realtime.connections` | Observable up-down counter | `{connection}` | — |
| `weblogic.notifications.queue_length` | Observable up-down counter | `{notification}` | — |
| `weblogic.audit.queue_length` | Observable up-down counter | `{entry}` | — (only with the built-in `WebAuditLog`) |
| `weblogic.audit.dropped` | Observable counter | `{entry}` | — |

- **Route requests** are WebLogic routes only (pages, APIs, fallback); requests a security stage stops
  never reach a route and are counted as rejections instead. The duration covers route middleware, the
  handler and the result (template render included). A handler exception counts as `5xx`.
- `http.route` is the route **pattern** (`/blog/{slug}`, `{**path}` for the fallback), never the path.
  `http.request.method` is `_OTHER` for non-standard methods. `weblogic.status_class` is `2xx`, `4xx`…
- `weblogic.security.reason` is the stage's reason **code** without its variable part:
  `country-denied:DK` is recorded as `country-denied`, `forbidden:group:admin` as `forbidden`,
  `route-rate-limit:tiny` as `route-rate-limit`. Reasons that are not a short lower-case code become `other`.
- `weblogic.rate_limit.scope`: `global` (global limit), `route` (route policy), `realtime` (connection
  limits), `sign_in` (login throttle), or the name of an app stage that answered `429`.
- `weblogic.auth.outcome`: `succeeded`, `failed`, `two_factor_required`, `locked` (an account was just
  locked). `weblogic.auth.method` lists the methods used (`pwd`, `pwd,otp`, `passkey`, `link`,
  `ext:google`; app-chosen method names become `other`); failures carry `weblogic.auth.reason`
  (`invalid_credentials`, `locked_out`, `not_allowed`, `throttled`, `suspended`) instead. Refreshing
  or re-signing a user (`RefreshAsync`, password change) is not counted. Re-authentication is.

Tags never contain client addresses, user ids, user names, paths, query strings, tokens or config
values, so cardinality stays low and exported metrics are not personal data.

Sign-in, rejection and realtime counts come from the events WebLogic publishes on the CodeLogic
event bus (`WebSecurityRejectedEvent`, `UserSignedInEvent`, `SecondFactorRequiredEvent`,
`SignInFailedEvent`, `UserLockedOutEvent`, `RealtimeConnectionRejectedEvent`), via the hosted
`WebLogicMetricsRecorder`.

## Spans

| Span | When | Tags |
|---|---|---|
| `weblogic.security.pre_routing` | the pre-routing security stages of a request | `weblogic.security.outcome` (`passed`/`rejected`); on rejection also `weblogic.security.stage`, `weblogic.security.reason`, `http.response.status_code` |
| `weblogic.security.endpoint` | the endpoint security stages | same |
| `weblogic.template.render` | each `IWebTemplateRenderer.RenderAsync` (pages, widget templates) | `weblogic.template.path` |

The spans are children of ASP.NET Core's request span, so they appear inside the request trace.
ASP.NET Core's own `http.server.request.duration` metric and request span are still the place for
whole-request numbers; WebLogic's instruments add what only WebLogic knows.

## Wiring OpenTelemetry

Add the OpenTelemetry packages to the **app** (for example `OpenTelemetry.Extensions.Hosting` and an
exporter such as `OpenTelemetry.Exporter.OpenTelemetryProtocol`) and name WebLogic's meter and source:

```csharp
using CL.WebLogic.Diagnostics.Models;

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddMeter(WebLogicInstruments.MeterName)
        .AddOtlpExporter())
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddSource(WebLogicInstruments.ActivitySourceName)
        .AddOtlpExporter());
```

Without OpenTelemetry, watch the counters live with
`dotnet-counters monitor -n <process> --counters CodeLogic.WebLogic`.

## Testing

`WebLogicMetrics.Meter` is the host's meter instance (created through the host's `IMeterFactory`),
so a `MeterListener` in a test can listen to one host only:

```csharp
var meter = host.Services.GetRequiredService<WebLogicMetrics>().Meter;
using var listener = new MeterListener
{
    InstrumentPublished = (instrument, l) =>
    {
        if (ReferenceEquals(instrument.Meter, meter))
            l.EnableMeasurementEvents(instrument);
    }
};
listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => { /* assert */ });
listener.Start();
```

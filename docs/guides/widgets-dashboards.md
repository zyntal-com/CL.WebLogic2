# Widgets and dashboards

A widget is a small, independently rendered piece of a page: a clock, a "latest posts" list, a chart,
a note. Widgets can be written into templates, placed in named areas, or arranged by users on
dashboards. The toolkit supplies the tools; **where layouts and settings are saved is up to the app**.

## Widgets

```csharp
public Task ContributeAsync(WebContributionBuilder b, CancellationToken ct)
{
    b.MapWidget("clock", (context, ct) => ValueTask.FromResult(
        WebWidgetResult.Template("widgets/clock", new { Time = DateTime.UtcNow.ToString("HH:mm") })), o =>
    {
        o.AllowAnonymous();
        o.Description = "Server time";
        o.RefreshSeconds = 30;                      // the client re-fetches it
    });

    b.MapWidget("notes", async (context, ct) =>
    {
        var settings = await context.GetSettingsAsync(ct);
        return WebWidgetResult.Template("widgets/notes", new { Text = settings.GetValueOrDefault("text", ""), context.InstanceId });
    }, o => o.Action("save", SaveNoteAsync));      // POST /_weblogic/widgets/notes/actions/save

    b.PlaceWidget("sidebar", "clock", o => o.Order = 1);   // {widgetarea:sidebar}
    return Task.CompletedTask;
}
```

- Widgets require a signed-in user unless `AllowAnonymous()`, or set `o.Access` to a groups/permissions
  rule. Placements can add their own access rule; it is enforced by the widget endpoint too. When every
  placement of a widget has a rule, callers need one of them to use the widget anywhere, dashboards
  included, and dashboard items cannot reuse a placement's instance id.
- In templates: `{widget:clock}`, `{widget:latest-posts count="5"}`, `{widgetarea:sidebar}`.
- Refresh and actions go through `/_weblogic/widgets/{name}` and `/_weblogic/widgets/{name}/actions/{action}`
  (CSRF-protected). **Widget parameters arrive from the query string and are untrusted**: validate them
  like any other input.

## Widget settings

`context.GetSettingsAsync` / `SaveSettingsAsync` read and write per-instance values through
`IWebWidgetSettingsStore` (`web.UseWidgetSettingsStore<T>()`). Settings are always keyed by
**owner and instance**; the owner comes from the server-side `IWebDashboardOwnerResolver`, never from
the request, so one user cannot read or overwrite another user's widget settings by guessing an
instance id. `SaveSettingsAsync` returns `false` when there is no owner (an anonymous visitor with the
default resolver); answer that with a 401.

## Dashboards

A dashboard is a list of `WebDashboardItem`s (instance id, widget, zone, order, parameters) stored as a
`WebDashboardLayout` with a version number for optimistic concurrency.

```csharp
web.UseDashboardStore<MyLayoutStore>()               // IWebDashboardLayoutStore: GetAsync / SaveAsync(layout, expectedVersion)
   .UseDashboardDefaults<MyDashboardDefaults>()      // what a new dashboard contains
   .UseDashboardOwnerResolver<TeamOwnerResolver>();  // optional: who owns a dashboard

// a page (dashboards: WebDashboardService)
var html = await dashboards.RenderAsync(r.HttpContext, "home", ["main", "side"], ct);
return WebResult.Page("templates/dashboard", new { Dashboard = new WebHtml(html) });
```

```html
<button type="button" data-wl-dashboard-edit="home">Edit layout</button>
{model:dashboard}
```

The client handles editing (drag and drop plus keyboard moves, add, remove) and saves through
`/_weblogic/dashboard/{key}`. `WebDashboardLayouts` has the helpers your own code can use:
`Normalize`, `Move`, `Add`, `Remove`, `IsValidKey`, `NewInstanceId` and `DefaultInstanceId` (stable
ids for default items, so their settings survive the first save).

### Who owns a dashboard

The default `UserDashboardOwnerResolver` gives every signed-in user their own dashboards. Nothing in
the toolkit assumes that, though:

| Want | Resolver returns |
|---|---|
| Per-user dashboards (default) | `"user:"` + a hash of the user id |
| One shared team dashboard | `"team:" + teamId` from the identity |
| A per-tenant dashboard | `"tenant:" + tenantId` |
| A read-only kiosk | a fixed key, and the store refuses saves |

Keys must match `^[A-Za-z0-9_.:-]{1,128}$`, and `..` is rejected.

### Storage choices

JSON files (`samples/StarterWebsite/Infrastructure/JsonDashboardLayoutStore.cs`), MySQL (MiniBlog), a
document database, or nothing at all (the in-memory store in Development). Implement `SaveAsync` with
a compare on `expectedVersion` so two tabs cannot silently overwrite each other.

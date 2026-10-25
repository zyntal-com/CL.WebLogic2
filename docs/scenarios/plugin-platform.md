# Plugin-heavy platform

A host application that is mostly a shell, with features delivered as CodeLogic plugins: a shop
module, a forum module, a reporting module, each deployed and updated on its own.

## How it fits together

- The host registers the shared services (stores, notifier, theme) and the layout.
- Each plugin implements `IPlugin` and `IWebContributor`, and contributes its routes, forms, widgets,
  templates and realtime bridges in `ContributeAsync` ([Plugins](../guides/plugins.md)).
- WebLogic registers a plugin's contributions when it loads and removes them all when it unloads; the
  routing table updates without a restart.

```csharp
public Task ContributeAsync(WebContributionBuilder b, CancellationToken ct)
{
    b.MapTemplates(typeof(ForumPlugin).Assembly);           // theme/forum/*.html inside the plugin
    b.MapForm<NewTopicForm>();
    b.MapFormOptions(new ForumCategoryOptions(b.Services.GetRequiredService<ForumStore>()));

    var forum = b.MapGroup("/forum");
    forum.MapPage("", IndexAsync).AllowAnonymous().WithOutputCache("forum");
    forum.MapPage("/new", NewTopicAsync, "POST").WithRateLimit("posting");

    b.MapWidget("latest-topics", LatestTopicsAsync, o => o.AllowAnonymous());
    b.PlaceWidget("sidebar", "latest-topics");
    return Task.CompletedTask;
}
```

## Boundaries

| A plugin can | A plugin cannot |
|---|---|
| Add routes, groups, forms, options, validators, widgets, placements, templates, page scripts, realtime bridges | Add request-level security stages |
| Use host services from `b.Services` | Replace forms or templates the host registered |
| Ship templates the site theme can override | Change security settings or anonymous access of routes it does not own |

Plugin routes get the same defaults as host routes: sign-in required unless `AllowAnonymous()`, CSRF,
rate limits, and they appear in the explorer with the plugin as their owner.

## Theming plugins

Plugins ship default templates as embedded resources (`theme/forum/index.html` → `forum/index`). The
site theme overrides any of them by placing a file with the same path, so a designer can restyle a
plugin without touching its code. Plugin templates use the host's layouts (`{layout:layouts/main}`)
and partials.

## Data

Each plugin owns its persistence: its own tables, files or service. Share infrastructure (a database
connection, a storage account) through host services, not through WebLogic.

## Packaging and deployment

Plugins compile against WebLogic but load the host's copy (`ExcludeAssets="runtime"`,
`CopyLocalLockFileAssemblies=false`). Deploy the plugin's assembly into the CodeLogic plugins folder;
`samples/Plugins/MiniBlog.Admin.Plugin` and the MiniBlog Dockerfile show the whole path.

## Unloading safely

Unloading removes routes immediately; requests in flight finish. Avoid holding references to plugin
types in host statics or caches, so the plugin's load context can be collected (WebLogic's own caches
use weak references for this).

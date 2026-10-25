# Plugins

A CodeLogic plugin that also implements `IWebContributor` contributes web features as soon as it
loads, and they disappear when it unloads. The host needs no code for it.

## A web plugin

```csharp
public sealed class AdminPlugin : IPlugin, IWebContributor
{
    public PluginManifest Manifest { get; } = new() { Id = "shop.admin", Name = "Shop admin", Version = "1.0.0" };
    // ... the usual IPlugin lifecycle members ...

    public Task ContributeAsync(WebContributionBuilder b, CancellationToken ct)
    {
        var orders = b.Services.GetRequiredService<OrderService>();   // host services

        b.MapTemplates(typeof(AdminPlugin).Assembly);                 // embedded theme/*.html
        b.MapForm<OrderEditForm>();

        var admin = b.MapGroup("/admin").RequireGroups("admin");
        admin.MapPage("/orders", (r, ct) => ListAsync(orders, r, ct));

        b.MapWidget("open-orders", OpenOrdersAsync, o => o.Access = new WebAccessRule { Groups = ["admin"] });
        b.PlaceWidget("sidebar", "open-orders");
        b.BridgeEvent<OrderPlacedEvent>("shop.order-placed", _ => WebRealtimeAudience.Group("admin"), e => new { e.OrderId });
        return Task.CompletedTask;
    }
}
```

What a plugin can contribute: routes and route groups, forms and options providers, form validators,
widgets and placements, templates, page scripts and realtime bridges. Everything is tracked by a
contribution handle and removed on unload; routes stop answering at once (the endpoint data source
changes), and the plugin's load context can be collected.

What a plugin **cannot** do: add request-level security stages, replace a form or template the
application registered, or turn off security for routes it does not own. Plugin routes get the same
defaults as app routes (signed-in users only, CSRF, rate limits).

## Templates in the plugin

```xml
<ItemGroup>
  <EmbeddedResource Include="theme\**\*.html" LogicalName="theme/%(RecursiveDir)%(Filename)%(Extension)" />
</ItemGroup>
```

`b.MapTemplates(assembly)` registers every `theme/...html` resource under its path (`theme/admin/index.html`
becomes `admin/index`). The site theme can override any of them by shipping a file with the same path.
`b.MapTemplate(path, source)` adds one from a string.

## Packaging

Compile against WebLogic but use the host's copy at runtime, so the types match:

```xml
<PackageReference Include="CodeLogic" ExcludeAssets="runtime" PrivateAssets="all" />
<PackageReference Include="CodeLogic.WebLogic" ExcludeAssets="runtime" PrivateAssets="all" />
<PropertyGroup>
  <CopyLocalLockFileAssemblies>false</CopyLocalLockFileAssemblies>
</PropertyGroup>
```

Deploy the plugin's own assembly (and only its private dependencies) to the CodeLogic plugins folder.
`samples/Plugins/MiniBlog.Admin.Plugin` is the complete example: admin CRUD on forms, a dashboard
widget, templates shipped inside the plugin.

## Application contributors

Contributors registered with `web.AddContributor<T>()` use the same builder; they simply live as long
as the app. Give them a `WebContributorDescriptor` (id, name, kind) so the explorer can show who
registered what.

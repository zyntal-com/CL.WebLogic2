# SaaS, multi-tenant

One deployment serving many customers ("tenants"), each with their own users and data, on their own
subdomain or custom domain. The rule: **nothing may cross a tenant boundary** — not a page, a cached
response, a realtime event or a rate-limit bucket.

## Resolving the tenant

```csharp
// simple: a fixed host map
web.UseHostTenants(new Dictionary<string, string>
{
    ["acme.app.example"] = "acme",
    ["globex.app.example"] = "globex"
});

// usual: your own resolver (subdomain, custom domains from your database, a path prefix)
web.UseTenantResolver<DomainTenantResolver>();

public sealed class DomainTenantResolver(TenantDirectory tenants) : IWebTenantResolver
{
    public async ValueTask<string?> ResolveAsync(WebSecurityContext context, CancellationToken ct) =>
        (await tenants.FindByHostAsync(context.HttpContext.Request.Host.Host, ct))?.Id;
}
```

The `tenant` stage runs after the host filter (so `allowedHosts` should contain your wildcard domains
or be filled from the same directory). The tenant is then part of the request's security context.

## Where WebLogic applies the tenant

| Area | Behaviour |
|---|---|
| Sign-in | A user with a `TenantId` is treated as unknown on another tenant |
| Identity | `request.Identity.TenantId` |
| Authorization | `RequireSameTenant()` in policies |
| API keys | A key bound to a tenant only works on that tenant |
| Rate limits | `"partition": "tenant"` policies give each tenant its own bucket (one noisy customer cannot exhaust another's) |
| Output cache | Keys vary by tenant automatically |
| Realtime | `PublishToTenantAsync(tenantId, ...)` reaches only that tenant's connections |

## Where you apply it

- **Every query.** Filter by tenant in your stores and services; take the tenant from
  `request.Identity.TenantId` (signed in) or the security context, never from a form field.
- **User store.** `FindByLoginAsync` may return users of other tenants (WebLogic rejects them), but it is
  simpler and faster to scope the lookup to the current tenant.
- **Fragment cache keys.** Include the tenant: `$"{tenant}:dashboard-stats"`.
- **Dashboard owners.** The default per-user owner key is fine; a "tenant wall" returns
  `"tenant:" + tenantId` from `IWebDashboardOwnerResolver`.
- **Theming per tenant.** Pass tenant branding (logo, colours) as template globals
  (`ITemplateGlobalsProvider` receives the `HttpContext`).
- **E-mail links.** `publicBaseUrl` is one URL; for per-tenant domains build links in your
  `IWebSecurityNotifier` from the tenant's domain.

## Realtime example

```csharp
await hub.PublishToTenantAsync(order.TenantId, "order.created",
    new { order.Id, order.Total }, WebRealtimeAudience.Group("sales"), cancellationToken: ct);
```

Only members of `sales` **in that tenant** receive it.

## Running at scale

Many tenants usually means several nodes: see [Cluster](cluster.md).

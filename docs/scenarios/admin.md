# Admin back office

An internal tool for staff: lists, edit forms, a dashboard, roles. Everything is behind sign-in, and
mistakes are expensive, so authorization and auditing matter more than caching.

## Roles and permissions

Put groups (roles) and permissions on your users (`WebUser.Groups`, `WebUser.Permissions`) or resolve
permissions from your own tables with `IWebPermissionResolver` (`web.UsePermissionResolver<T>()`;
resolved only when a rule needs them).

```csharp
web.AddAuthorizationPolicy("support", p => p.RequireAnyGroup("support", "admin"));

var admin = b.MapGroup("/admin").RequireGroups("staff")
    .WithLoginPath("/admin/login").WithAccessDeniedPath("/admin/denied");
admin.MapPage("/customers", ListCustomersAsync).RequirePolicy("support");
admin.MapPage("/customers/{id}/refund", RefundAsync, "POST").RequirePermissions("billing.refund");
```

Permission wildcards (`billing.*`) are supported. Handlers still check resource access themselves (a
support agent of region A editing a customer of region B).

## Sign-in hardening

- Require two-factor for staff and recent proof for dangerous actions:
  `admin.RequireRecentSignIn(TimeSpan.FromHours(8), "otp")` on the whole area, and
  `.RequireRecentSignIn(TimeSpan.FromMinutes(10), "pwd", "otp")` on refunds or user management. Staff
  without 2FA are sent to the admin login page; a "confirm it's you" form calls
  `request.Auth.ReauthenticateAsync(password, code)`.
- External sign-in (company SSO) goes through `SignInExternalAsync`, so the same two-factor rules apply.
- `auth.lockout`, the per-login throttle and the per-IP cap are on by default.
- Shorter sessions: `security.session.idleTimeoutMinutes: 30`, `absoluteTimeoutHours: 12`.
- Optionally limit by network: `security.ipRules.allow` with the office/VPN ranges (applies to the
  whole site), or a custom requirement for just `/admin`.
- `request.Auth.SignOutAsync(everywhere: true)` for "sign out all devices", `ListSessionsAsync` /
  `RevokeSessionAsync` for a device list, `Session.MaxPerUser` to cap concurrent sessions and
  `NotifyNewDevice` for sign-in alerts; disabling a user ends their sessions at the next stamp check.

## Forms

Edit forms use `request.Forms.Prefill(model)` and `BindAsync<T>()`; `IWebFormValidator<T>` covers
uniqueness checks. Options providers with a `Policy` keep lookup endpoints (customers, products) for
staff only. Wizards fit multi-step operations (onboarding a customer) without server-side state.

## Dashboard

A staff dashboard with widgets (open tickets, sales today, system health) on
`WebDashboardService.RenderAsync`. Per-user layouts by default; return a shared owner key from your
`IWebDashboardOwnerResolver` for a team wall. Widgets with `o.Access` only appear for staff who may
see them.

## Audit

```csharp
web.UseAuditSink<DatabaseAuditSink>();
```

```json
"security": { "audit": { "enabled": true, "ipHashKey": "<secret>" } }
```

Sign-ins, failures, lockouts, 2FA changes, API key use and every rejected request reach your sink.
Add your own domain events ("refund issued") to the same table from your handlers.

## Explorer

Leave the explorer off in production, or enable it with `explorer.inProduction` for holders of
`weblogic.explorer` only. Its anonymous-route list is a quick review that nothing in `/admin` is open.

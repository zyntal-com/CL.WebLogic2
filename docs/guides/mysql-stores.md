# MySQL stores (add-on)

`CodeLogic.WebLogic.MySql` is an optional package with production implementations of every WebLogic
store contract on CL.MySQL2. The core toolkit stays database-agnostic; reference this package only
when MySQL is your choice.

```csharp
// CL.MySQL2 must be loaded and configured (config.mysql.json → databases → "Default").
builder.AddCodeLogicWebLogic(web => web
    .UseMySqlStores("Default", o =>
    {
        o.Stores = WebMySqlStores.All & ~WebMySqlStores.ApiKeys;   // keep your own API key store
        o.AuditRetention = TimeSpan.FromDays(30);
    }));

builder.Services.AddDataProtection()
    .SetApplicationName("my-site")
    .PersistKeysToMySql("Default")
    .ProtectKeysWithSecret("KEYRING_SECRET");     // AES-256-GCM at rest (optional, recommended)
```

`UseMySqlStores` replaces the in-memory defaults for the selected stores, so the development-store
guard and the cluster readiness checks pass. Stores left out keep whatever the app registered.

## Tables

| Flag | Contract | Table |
|---|---|---|
| `Users` | `IWebUserStore` (+ lockout, e-mail, external logins) | `wl_users`, `wl_user_logins` |
| `Sessions` | `IWebSessionStore` (+ device listing) | `wl_sessions` |
| `ApiKeys` | `IWebApiKeyStore` | `wl_api_keys` |
| `RecoveryCodes` | `IWebRecoveryCodeStore` | `wl_recovery_codes` |
| `OneTimeUse` | `IWebOneTimeUseStore` | `wl_one_time_use` |
| `LoginThrottle` | `IWebLoginThrottle` | `wl_login_throttle` |
| `RateLimits` | `IWebRateLimitStore` | `wl_rate_limits` |
| `Audit` | `IWebAuditSink` | `wl_audit` |
| `WidgetSettings` | `IWebWidgetSettingsStore` | `wl_widget_settings` |
| `Dashboards` | `IWebDashboardLayoutStore` | `wl_dashboards` |
| (DataProtection) | `IXmlRepository` | `wl_dataprotection_keys` |

All tables use `utf8mb4_bin`; case-insensitive lookups go through normalized columns. With
`SyncSchemaOnStart` (default) the tables are created or updated before any other hosted service
starts. Turn it off when migrations are run separately; `SyncSchemaAsync` is still available on CL.MySQL2.

## Guarantees across nodes

- **Single use:** recovery codes are consumed with one `DELETE` (exactly one node succeeds); spent TOTP
  steps and single-use links use `INSERT IGNORE`.
- **Lockout:** failed sign-ins are counted and the lockout set in one `UPDATE`, so parallel attempts on
  several nodes cannot skip the limit.
- **Rate limits:** each token bucket is refilled and charged in a single round trip; the capacity holds
  exactly under concurrency.
- **Dashboards:** saves carry the expected version; a stale save fails instead of overwriting.
- **Key ring:** keys are inserted, never updated; every node sees every key.

## Housekeeping

`WebMySqlSweepService` deletes expired sessions, one-time keys, throttle and rate-limit rows, and audit
rows older than `AuditRetention`, every `SweepInterval` (10 minutes). Each node may run it; the deletes
are idempotent.

## Testing

The package's own tests run against a real server when `WEBLOGIC_MYSQL_TEST_HOST` (and `_PORT`, `_DB`,
`_USER`, `_PASS`) are set, and skip otherwise. CI provides a `mysql:8.4` service on Linux.

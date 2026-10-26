# CodeLogic.WebLogic.MySql

Production MySQL stores for [CodeLogic.WebLogic](https://www.nuget.org/packages/CodeLogic.WebLogic), on
CL.MySQL2. One call replaces every in-memory default, so the app can run on several nodes:

```csharp
// CL.MySQL2 must be loaded (config.mysql.json → databases → "Default").
builder.AddCodeLogicWebLogic(web => web
    .UseMySqlStores("Default"));               // or: o => o.Stores = WebMySqlStores.All & ~WebMySqlStores.Users

builder.Services.AddDataProtection()
    .SetApplicationName("my-site")
    .PersistKeysToMySql("Default")
    .ProtectKeysWithSecret("KEYRING_SECRET");
```

| Contract | Table | Notes |
|---|---|---|
| `IWebUserStore` (+ lockout, e-mail, external logins) | `wl_users`, `wl_user_logins` | Unique user names and e-mails (case-insensitive); failed sign-ins counted atomically |
| `IWebSessionStore` (+ listing) | `wl_sessions` | Keyed by the SHA-256 of the session id |
| `IWebApiKeyStore` | `wl_api_keys` | Hashes only |
| `IWebRecoveryCodeStore` | `wl_recovery_codes` | Consumed with a single `DELETE` |
| `IWebOneTimeUseStore` | `wl_one_time_use` | `INSERT IGNORE`: one winner per key across nodes |
| `IWebLoginThrottle` | `wl_login_throttle` | Same back-off as the in-memory throttle |
| `IWebRateLimitStore` | `wl_rate_limits` | Token buckets, refilled and charged in one atomic update-and-read |
| `IWebAuditSink` | `wl_audit` | Retention via `AuditRetention` (90 days) |
| `IWebWidgetSettingsStore`, `IWebDashboardLayoutStore` | `wl_widget_settings`, `wl_dashboards` | Optimistic versions for dashboards |
| `IWebPasskeyStore` | `wl_passkeys` | Unique credential ids; the signature counter is a compare-and-swap |
| DataProtection key ring | `wl_dataprotection_keys` | `PersistKeysToMySql` |

Tables are created and updated at startup (`SyncSchemaOnStart`), and a background sweep deletes
expired rows (`SweepInterval`, 10 minutes). Stores fail the request when the database is
unreachable; they never report a missing session or key instead.

Every query goes through CL.MySQL2's typed API (no SQL text), so all values are sent as parameters.
The core package has no database dependency; this add-on is the only one that references CL.MySQL2.
Docs: https://zyntal-com.github.io/CL.WebLogic2/

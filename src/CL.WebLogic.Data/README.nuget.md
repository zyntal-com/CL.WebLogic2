# CodeLogic.WebLogic.Data

Database stores for [CodeLogic.WebLogic](https://www.nuget.org/packages/CodeLogic.WebLogic) on
[CodeLogic.Data](https://www.nuget.org/packages/CodeLogic.Data): MySQL, PostgreSQL, SQL Server or SQLite.
One call replaces every in-memory default, so the app can run on several nodes:

```csharp
// Load CL.Data and the provider you use (config.data.json → connections → "Default").
await Libraries.LoadAsync<DataLibrary>();
await Libraries.LoadAsync<MySqlDataLibrary>();   // or PostgreSqlDataLibrary, SqlServerDataLibrary, SqliteDataLibrary

builder.AddCodeLogicWebLogic(web => web
    .UseDataStores("Default"));               // or: o => o.Stores = WebDataStores.All & ~WebDataStores.Users

builder.Services.AddDataProtection()
    .SetApplicationName("my-site")
    .PersistKeysToDatabase("Default")
    .ProtectKeysWithSecret("KEYRING_SECRET");
```

| Contract | Table | Notes |
|---|---|---|
| `IWebUserStore` (+ lockout, e-mail, external logins) | `wl_users`, `wl_user_logins` | Unique user names and e-mails (case-insensitive); failed sign-ins counted atomically |
| `IWebSessionStore` (+ listing) | `wl_sessions` | Keyed by the SHA-256 of the session id |
| `IWebApiKeyStore` | `wl_api_keys` | Hashes only |
| `IWebRecoveryCodeStore` | `wl_recovery_codes` | Consumed with a single `DELETE` |
| `IWebOneTimeUseStore` | `wl_one_time_use` | Insert-ignore: one winner per key across nodes |
| `IWebLoginThrottle` | `wl_login_throttle` | Same back-off as the in-memory throttle |
| `IWebRateLimitStore` | `wl_rate_limits` | Token buckets, refilled and charged in one atomic update-and-read |
| `IWebAuditSink` | `wl_audit` | Retention via `AuditRetention` (90 days) |
| `IWebWidgetSettingsStore`, `IWebDashboardLayoutStore` | `wl_widget_settings`, `wl_dashboards` | Optimistic versions for dashboards |
| `IWebPasskeyStore` | `wl_passkeys` | Unique credential ids; the signature counter is a compare-and-swap |
| DataProtection key ring | `wl_dataprotection_keys` | `PersistKeysToDatabase` |

Tables are created and updated at startup (`SyncSchemaOnStart`) at the connection's `syncMode`;
changes the mode does not allow are logged, never applied. A background sweep deletes expired rows
(`SweepInterval`, 10 minutes). Stores fail the request when the database is unreachable; they never
report a missing session or key instead.

Every store reads the primary and never the CL.Data entity cache: sessions, lockouts and one-time
keys must not come from a lagging replica or a stale copy. Keys compare case-sensitively on every
database (binary collations on MySQL and SQL Server).

Every query goes through CL.Data's typed API (no SQL text), so all values are sent as parameters.
The core package has no database dependency; this add-on references only the provider-neutral
CodeLogic.Data core, and the app chooses the provider.
Docs: https://zyntal-com.github.io/CL.WebLogic2/

# Database stores (add-on)

`CodeLogic.WebLogic.Data` is an optional package with production implementations of every WebLogic
store contract on CL.Data (`CodeLogic.Data`). It runs on MySQL, PostgreSQL, SQL Server and SQLite. The
core toolkit stays database-agnostic, and this add-on references only the provider-neutral CL.Data
core: your app picks the provider.

## Setup on MySQL

Reference `CodeLogic.WebLogic.Data` and the provider package `CodeLogic.Data.MySql`. Load CL.Data and
the provider library before WebLogic:

```csharp
await Libraries.LoadAsync<DataLibrary>();
await Libraries.LoadAsync<MySqlDataLibrary>();

builder.AddCodeLogicWebLogic(web => web
    .UseDataStores("Default", o =>
    {
        o.Stores = WebDataStores.All & ~WebDataStores.ApiKeys;   // keep your own API key store
        o.AuditRetention = TimeSpan.FromDays(30);
    }));

builder.Services.AddDataProtection()
    .SetApplicationName("my-site")
    .PersistKeysToDatabase("Default")
    .ProtectKeysWithSecret("KEYRING_SECRET");     // AES-256-GCM at rest (optional, recommended)
```

The connection lives in CL.Data's config file, `Libraries/CL.Data/config.data.json`:

```json
{
  "connections": {
    "Default": {
      "provider": "mysql",
      "host": "db",
      "port": 3306,
      "database": "mysite",
      "username": "mysite",
      "password": "…",
      "syncMode": "production",
      "mySql": { "allowPublicKeyRetrieval": true }
    }
  }
}
```

`allowPublicKeyRetrieval` is only needed when MySQL 8 hands out its RSA key for
`caching_sha2_password` (for example over a private Docker network, as in `samples/docker`).

`UseDataStores` replaces the in-memory defaults for the selected stores, so the development-store
guard and the cluster readiness checks pass. Stores left out keep whatever the app registered. Both
`UseDataStores` and `PersistKeysToDatabase` default to the connection `"Default"`.

## Other databases

The stores are the same on every provider; only the provider package, the library you load and the
`provider` value change.

| Database | Package | Library | `provider` |
|---|---|---|---|
| MySQL | `CodeLogic.Data.MySql` | `MySqlDataLibrary` | `mysql` |
| PostgreSQL | `CodeLogic.Data.PostgreSql` | `PostgreSqlDataLibrary` | `postgresql` |
| SQL Server | `CodeLogic.Data.SqlServer` | `SqlServerDataLibrary` | `sqlserver` |
| SQLite | `CodeLogic.Data.Sqlite` | `SqliteDataLibrary` | `sqlite` |

See the CL.Data documentation for each provider's connection settings. SQLite is a single file on
one machine: fine for one node or for tests, not for a cluster.

## Options

`WebDataOptions` (`CL.WebLogic.Data.Models`):

| Option | Default | Meaning |
|---|---|---|
| `ConnectionName` | `"Default"` | The CL.Data connection (`config.data.json` → `connections`) |
| `Stores` | `WebDataStores.All` | Which stores to register; leave out the ones your app owns |
| `SyncSchemaOnStart` | `true` | Create and update the `wl_*` tables at startup |
| `SweepInterval` | 10 minutes | How often expired rows are deleted |
| `AuditRetention` | 90 days | Audit rows older than this are deleted (zero = keep forever) |

## Tables

| Flag | Contract | Table |
|---|---|---|
| `Users` | `IWebUserStore` (+ lockout, e-mail, external logins, deletion) | `wl_users`, `wl_user_logins` |
| `Sessions` | `IWebSessionStore` (+ device listing) | `wl_sessions` |
| `ApiKeys` | `IWebApiKeyStore` | `wl_api_keys` |
| `RecoveryCodes` | `IWebRecoveryCodeStore` | `wl_recovery_codes` |
| `OneTimeUse` | `IWebOneTimeUseStore` | `wl_one_time_use` |
| `LoginThrottle` | `IWebLoginThrottle` | `wl_login_throttle` |
| `RateLimits` | `IWebRateLimitStore` | `wl_rate_limits` |
| `Audit` | `IWebAuditSink` | `wl_audit` |
| `WidgetSettings` | `IWebWidgetSettingsStore` | `wl_widget_settings` |
| `Dashboards` | `IWebDashboardLayoutStore` | `wl_dashboards` |
| `Passkeys` | `IWebPasskeyStore` | `wl_passkeys` |
| (DataProtection) | `IXmlRepository` | `wl_dataprotection_keys` |

Every store goes through CL.Data's typed API (repositories, `Query<T>()` with typed updates and
deletes, upserts, insert-ignore, `UpdateAndGetAsync`, transactions); the add-on contains no SQL text,
so every value is sent as a parameter.

## Schema sync

With `SyncSchemaOnStart` (default) the tables are created or updated before any other hosted service
starts (DataProtection reads the key ring at startup). The sync follows the connection's `syncMode`:

- `developer` applies every change the model needs, including type changes. For development only.
- `production` (CL.Data's default) creates tables and adds columns and indexes, and only widens types.
  Anything else is logged as a warning (drift) and never applied.
- `migration` never changes the schema; only your migrations do.

A failed sync stops startup with the reason. Turn `SyncSchemaOnStart` off when migrations run
separately; the row types in `CL.WebLogic.Data.Entities` describe the tables.

## Consistency

- **Primary only.** Every store reads and writes the primary and never uses the CL.Data entity cache:
  sessions, lockouts and one-time keys must not come from a lagging replica or a stale copy.
- **Case-sensitive keys on every database.** Session hashes, API key prefixes, credential ids and login
  keys compare exactly: `utf8mb4_bin` on MySQL (every `wl_*` table), `Latin1_General_100_BIN2` on SQL
  Server (key and lookup columns); PostgreSQL and SQLite compare exactly by default. Case-insensitive
  lookups (user names, e-mails) go through normalized columns.
- **Failures fail the request.** A store that cannot reach its database, or whose transaction commit
  fails, throws. It never reports a missing session or key, or assumes a write that was rolled back.

## Guarantees across nodes

- **Single use:** recovery codes are consumed with one `DELETE` (exactly one node succeeds); spent TOTP
  steps and single-use links use insert-ignore (one winner per key).
- **Lockout:** failed sign-ins are counted and the lockout set in one `UPDATE`, so parallel attempts on
  several nodes cannot skip the limit.
- **Rate limits:** each request refills and charges its bucket in one atomic update that returns the
  result (the row is locked meanwhile), so the capacity holds exactly under concurrency on any number
  of nodes.
- **Sign-in throttle:** each failure is one atomic `UPDATE` that increments or restarts the counter; a
  missing counter is created with insert-ignore.
- **Recovery code sets** are replaced in one transaction: never empty, never old and new codes mixed.
- **Dashboards:** saves carry the expected version; a stale save fails instead of overwriting.
- **Passkeys:** a credential id has one owner (insert-ignore), and each sign-in updates the counter only
  if it still holds the value that was read, so two nodes can never both accept the same assertion.
- **Key ring:** keys are inserted, never updated; every node sees every key.

The stores do not rely on provider quirks such as MySQL's left-to-right `SET` or changed-rows counts,
so they behave the same on every database.

## Housekeeping

`WebDataSweepService` deletes spent one-time keys, expired throttle rows, idle rate-limit buckets, and
audit rows older than `AuditRetention`, every `SweepInterval`. Expired sessions are swept by the
toolkit's session sweeper. Each node may run it; the deletes are idempotent.

## Upgrading from `CodeLogic.WebLogic.MySql`

The tables keep their names, but CL.Data's default column types differ from CL.MySQL2's on MySQL. See
[Upgrading to 5.0](upgrading-to-5.md#existing-mysql-databases).

## Testing

The package's own tests run every store on SQLite always, and on MySQL, PostgreSQL and SQL Server when
`WEBLOGIC_TEST_MYSQL_CS`, `WEBLOGIC_TEST_PG_CS` and `WEBLOGIC_TEST_MSSQL_CS` hold connection strings.
CI provides `mysql:8.4`, `postgres:18` and SQL Server 2022 services on Linux.

# Upgrading to 5.0

WebLogic 5.0 follows CodeLogic 5.0. It is for apps on WebLogic 4.9 (`CodeLogic.WebLogic` 4.9.x).
There is no compatibility layer: the changes below are breaking, and most of them come from CodeLogic 5
itself. Coming from the old 0.x line, read [Migrating from 0.x](migrating.md) first.

## Requirements

- **.NET 11 SDK.** Every package targets `net11.0` and is built with C# 15.
- **CodeLogic 5 packages.** WebLogic 5.0 needs `CodeLogic` 5.x and the matching `CodeLogic.*` library
  packages (CL.Mail, CL.Storage, CL.TwoFactorAuth, CL.Data, …). Move all CodeLogic and WebLogic packages
  together; a 4.9 package next to a 5.0 one does not work.
- While CodeLogic 5.0 is in preview, WebLogic 5.0 ships `-preview` packages too.

## Breaking changes

### `CodeLogic.WebLogic.MySql` is now `CodeLogic.WebLogic.Data`

CL.MySQL2 does not exist in CodeLogic 5. The database stores move to the provider-neutral CL.Data core
(`CodeLogic.Data`) and run on MySQL, PostgreSQL, SQL Server or SQLite. The app references one provider
package and loads its library next to CL.Data:

```csharp
await Libraries.LoadAsync<DataLibrary>();
await Libraries.LoadAsync<MySqlDataLibrary>();   // or PostgreSqlDataLibrary, SqlServerDataLibrary, SqliteDataLibrary
```

Replace the package reference `CodeLogic.WebLogic.MySql` with `CodeLogic.WebLogic.Data` plus a provider
(`CodeLogic.Data.MySql`, `.PostgreSql`, `.SqlServer` or `.Sqlite`), and rename:

| 4.9 | 5.0 |
|---|---|
| `web.UseMySqlStores(connectionId, configure)` | `web.UseDataStores(connectionName = "Default", configure)` |
| `AddDataProtection().PersistKeysToMySql(connectionId)` | `AddDataProtection().PersistKeysToDatabase(connectionName = "Default")` |
| `WebMySqlOptions` | `WebDataOptions` |
| `WebMySqlOptions.ConnectionId` | `WebDataOptions.ConnectionName` |
| `WebMySqlStores` | `WebDataStores` (same flags) |
| `MySqlWebUserStore`, `MySqlWebSessionStore`, … | `DataWebUserStore`, `DataWebSessionStore`, … |
| `WebMySqlSweepService` | `WebDataSweepService` |
| `CL.WebLogic.MySql.*` namespaces | `CL.WebLogic.Data.*` (`Hosting`, `Models`, `Stores`, `Services`, `Entities`) |

`Stores`, `SyncSchemaOnStart`, `SweepInterval` and `AuditRetention` keep their names and defaults. The
tables keep their `wl_*` names. See [Database stores](data-stores.md) for the full page.

### CL.Data config instead of `config.mysql.json`

The connection now lives in `Libraries/CL.Data/config.data.json` under `connections`, with camelCase
keys:

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

Copy host, port, database and credentials over from your old `config.mysql.json`. The connection name
(`"Default"` here) is what you pass to `UseDataStores` and `PersistKeysToDatabase`.

### Existing MySQL databases

CL.Data's default column types on MySQL differ from CL.MySQL2's:

| Column kind | CL.MySQL2 (4.9) | CL.Data (5.0) |
|---|---|---|
| Date and time (`*_utc`, `at_utc`) | `DATETIME` | `DATETIME(6)` |
| JSON and long text (`record_json`, `data_json`, `xml`, `credential_id`) | `TEXT` / `MEDIUMTEXT` | `LONGTEXT` |

The startup schema sync follows the connection's `syncMode`:

- Under `production` these changes are not "widening" in CL.Data's rules, so they are **logged as
  drift and not applied**. The app runs, and the warning repeats at every start.
- Under `developer` the columns are altered at startup.

To bring an existing database in line, back it up first, then either:

1. set `syncMode` to `developer` for one start, let the sync alter the columns, and set it back to
   `production`; or
2. alter the columns yourself (the drift warning names each table and change; the row types in
   `CL.WebLogic.Data.Entities` list every column).

Keys stay case-sensitive: the tables still use `utf8mb4_bin`.

### Results and errors

CodeLogic 5 makes `Error` a closed hierarchy (`ValidationError`, `UnauthorizedError`, `NotFoundError`,
`ConflictError`, `UnavailableError`, `TimeoutError`, `InternalError`). Switches over it must be
exhaustive; WebLogic builds with CS8509 as an error, and so can you.

- `WebResult.Problem(result.Error)` turns a failed `Result` into a response whose status follows the
  error's kind (`WebErrorStatus.For`: 400, 401, 404, 409, 503, 504, 500). An internal error's message is
  never sent. See [Routing and results](routing.md#results).
- Pick the `Error` factory by kind in your own stores and services (`Error.Conflict`, `Error.NotFound`,
  …); the status follows.
- Compare auth error codes against the `WebAuthErrorCodes` constants (`CL.WebLogic.Auth.Accounts`), not
  string literals or `Contains`. See [Authentication](authentication.md).

### Plugins are isolated

CodeLogic 5 loads each plugin in its own context. A type that crosses the host/plugin boundary (a
WebLogic contract such as `IWebContributor`, a shared service, CL.Data rows) must come from one copy
of its assembly, or the host and the plugin see two different types. List those assemblies in
`PluginOptions.SharedAssemblies`; the plugin then always gets the host's copy, even if it ships one.
MiniBlog does this in `samples/MiniBlog/Program.cs`:

```csharp
var pluginOptions = new PluginOptions { PluginsDirectory = pluginsFolder };
foreach (var shared in new[] { "CL.WebLogic", "MiniBlog.Data", "CL.Data" })
    pluginOptions.SharedAssemblies.Add(shared);
var plugins = new PluginManager(CodeLogic.CodeLogic.GetEventBus(), pluginOptions);
```

At least `CL.WebLogic` belongs there for any plugin that contributes web features.

### Smaller changes

- **Strong naming.** Every WebLogic assembly is strong-named with the shared CodeLogic key. Nothing to
  do unless you refer to WebLogic assemblies by full name.
- **Sealed events.** `WebLogicConfigAppliedEvent`, `WebLogicRestartRequiredEvent`,
  `WebContributorRegisteredEvent` and `WebContributorRemovedEvent` are sealed records now.

## Checklist

1. Install the .NET 11 SDK and target `net11.0`.
2. Move every `CodeLogic*` package to 5.x in one step.
3. Replace `CodeLogic.WebLogic.MySql` with `CodeLogic.WebLogic.Data` and a `CodeLogic.Data.*` provider;
   load `DataLibrary` and the provider library before WebLogic.
4. Rename `UseMySqlStores` → `UseDataStores`, `PersistKeysToMySql` → `PersistKeysToDatabase`, and the
   `WebMySql*` / `MySqlWeb*` types and namespaces.
5. Write `Libraries/CL.Data/config.data.json` from your old `config.mysql.json`.
6. On an existing MySQL database: back up, then apply the column-type changes (one `developer` start,
   or by hand) and check that the drift warning is gone.
7. Make switches over `Error` exhaustive; return `WebResult.Problem(error)` for failed results; compare
   auth codes with `WebAuthErrorCodes`.
8. List shared assemblies (at least `CL.WebLogic`) in `PluginOptions.SharedAssemblies` if you load
   plugins.

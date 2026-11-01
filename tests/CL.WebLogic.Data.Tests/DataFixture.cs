using CL.Data.Hosting;
using CL.Data.MySql;
using CL.Data.PostgreSql;
using CL.Data.Schema;
using CL.Data.Sqlite;
using CL.Data.SqlServer;
using CL.WebLogic.Data.Entities;
using CL.WebLogic.Data.Models;
using CL.WebLogic.Data.Services;
using CL.WebLogic.Hosting.Services;
using CodeLogic;

namespace CL.WebLogic.Data.Tests;

/// <summary>
/// Boots CodeLogic with CL.Data once per test run, with one connection per database: SQLite always
/// (a file in the temp folder), MySQL, PostgreSQL and SQL Server when their connection string is set
/// (<c>WEBLOGIC_TEST_MYSQL_CS</c>, <c>WEBLOGIC_TEST_PG_CS</c>, <c>WEBLOGIC_TEST_MSSQL_CS</c>). The
/// <c>wl_*</c> tables are dropped and created with the same schema sync the app uses.
/// </summary>
public sealed class DataFixture : IAsyncLifetime
{
    private static readonly (string Name, string Variable)[] Servers =
    [
        ("mysql", "WEBLOGIC_TEST_MYSQL_CS"),
        ("postgresql", "WEBLOGIC_TEST_PG_CS"),
        ("sqlserver", "WEBLOGIC_TEST_MSSQL_CS"),
    ];

    private readonly Dictionary<string, WebDataDatabase> _databases = new(StringComparer.Ordinal);
    private string _root = "";

    /// <summary>The databases the tests run on: SQLite plus every configured server.</summary>
    public static IReadOnlyList<string> Configured { get; } =
        ["sqlite", .. Servers.Where(s => ConnectionString(s.Variable) is not null).Select(s => s.Name)];

    /// <summary>xunit member data: one row per configured database.</summary>
    public static TheoryData<string> Providers => [.. Configured];

    /// <summary>The store database for <paramref name="provider"/>.</summary>
    public WebDataDatabase Db(string provider) => _databases[provider];

    public async Task InitializeAsync()
    {
        _root = Path.Combine(Path.GetTempPath(), "wl-data-tests-" + Guid.NewGuid().ToString("N")[..8]);
        var init = await CodeLogic.CodeLogic.InitializeAsync(o =>
        {
            o.FrameworkRootPath = _root;
            o.AppVersion = "1.0.0";
            o.HandleShutdownSignals = false;
        });
        if (!init.Success)
            throw new InvalidOperationException("CodeLogic init failed: " + init.Message);

        var configDir = Path.Combine(_root, "Libraries", "CL.Data");
        Directory.CreateDirectory(configDir);
        File.WriteAllText(Path.Combine(configDir, "config.data.json"), Config());

        await Libraries.LoadAsync<DataLibrary>();
        await Libraries.LoadAsync<SqliteDataLibrary>();
        if (Configured.Contains("mysql")) await Libraries.LoadAsync<MySqlDataLibrary>();
        if (Configured.Contains("postgresql")) await Libraries.LoadAsync<PostgreSqlDataLibrary>();
        if (Configured.Contains("sqlserver")) await Libraries.LoadAsync<SqlServerDataLibrary>();
        await CodeLogic.CodeLogic.ConfigureAsync();
        await CodeLogic.CodeLogic.StartAsync();

        var resolver = new CodeLogicLibraryResolver();
        foreach (var provider in Configured)
        {
            var db = new WebDataDatabase(resolver, new WebDataOptions { ConnectionName = provider });
            _databases[provider] = db;
            // Every run starts from freshly created tables.
            foreach (var map in AllTables())
                WebDataDatabase.Value(await db.Db.Admin.DropTableAsync(map.GetNames(db.Db.NamingPolicy).Table));
            WebDataDatabase.Value(await db.Db.Schema.SyncAsync(AllTables(), new SchemaSyncOptions { Force = true }));
        }
    }

    /// <summary>Deletes every row of <typeparamref name="T"/>'s table.</summary>
    public async Task ClearAsync<T>(string provider) =>
        WebDataDatabase.Value(await Db(provider).Db.Admin.TruncateAsync<T>());

    public async Task DisposeAsync()
    {
        await CodeLogic.CodeLogic.StopAsync();
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // SQLite may still hold the file for a moment; the temp folder is cleaned eventually.
        }
    }

    /// <summary>The maps of every <c>wl_*</c> table.</summary>
    public static IReadOnlyList<CL.Data.Mapping.EntityMap> AllTables() =>
    [
        .. new[]
        {
            WebDataTables.Map<UserRow>(), WebDataTables.Map<UserLoginRow>(), WebDataTables.Map<SessionRow>(), WebDataTables.Map<ApiKeyRow>(),
            WebDataTables.Map<RecoveryCodeRow>(), WebDataTables.Map<OneTimeUseRow>(), WebDataTables.Map<LoginThrottleRow>(),
            WebDataTables.Map<RateLimitRow>(), WebDataTables.Map<AuditRow>(), WebDataTables.Map<WidgetSettingsRow>(),
            WebDataTables.Map<DashboardRow>(), WebDataTables.Map<DataProtectionKeyRow>(), WebDataTables.Map<PasskeyRow>(),
        }.Select(m => m.IsSuccess ? m.Value! : throw new InvalidOperationException(m.Error?.Message))
    ];

    private string Config()
    {
        var sqlite = Path.Combine(_root, "weblogic.db").Replace("\\", "/", StringComparison.Ordinal);
        var connections = new List<string>
        {
            $$"""
            "sqlite": { "provider": "sqlite", "database": "{{sqlite}}", "syncMode": "developer" }
            """
        };
        foreach (var (name, variable) in Servers)
        {
            if (ConnectionString(variable) is { } cs)
                connections.Add($$"""
                    "{{name}}": { "provider": "{{name}}", "connectionString": "{{cs.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}}", "syncMode": "developer", "readPreference": "primary" }
                    """);
        }
        return $$"""
            {
              "enabled": true,
              "defaultConnection": "sqlite",
              "connections": { {{string.Join(",\n", connections)}} }
            }
            """;
    }

    private static string? ConnectionString(string variable) =>
        Environment.GetEnvironmentVariable(variable) is { Length: > 0 } value ? value : null;
}

[CollectionDefinition(Name)]
public sealed class DataTestGroup : ICollectionFixture<DataFixture>
{
    public const string Name = "data";
}

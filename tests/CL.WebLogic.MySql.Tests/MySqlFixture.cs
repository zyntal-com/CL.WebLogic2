using CL.MySQL2;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.MySql.Entities;
using CL.WebLogic.MySql.Models;
using CL.WebLogic.MySql.Services;
using CodeLogic;

namespace CL.WebLogic.MySql.Tests;

/// <summary>
/// Boots CodeLogic with CL.MySQL2 once per test run against the configured database and drops the
/// <c>wl_*</c> tables, then creates them with the same schema sync the app uses.
/// </summary>
public sealed class MySqlFixture : IAsyncLifetime
{
    private static string Env(string key, string fallback) =>
        Environment.GetEnvironmentVariable(key) is { Length: > 0 } value ? value : fallback;

    /// <summary>True when a test database is configured.</summary>
    public static bool Configured => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBLOGIC_MYSQL_TEST_HOST"));

    public static readonly Type[] AllTables =
    [
        typeof(UserRow), typeof(UserLoginRow), typeof(SessionRow), typeof(ApiKeyRow), typeof(RecoveryCodeRow), typeof(OneTimeUseRow),
        typeof(LoginThrottleRow), typeof(RateLimitRow), typeof(AuditRow), typeof(WidgetSettingsRow), typeof(DashboardRow), typeof(DataProtectionKeyRow), typeof(PasskeyRow)
    ];

    public WebMySqlDatabase Db { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        if (!Configured)
            return;
        var root = Path.Combine(Path.GetTempPath(), "wl-mysql-tests-" + Environment.ProcessId);
        var init = await CodeLogic.CodeLogic.InitializeAsync(o =>
        {
            o.FrameworkRootPath = root;
            o.AppVersion = "1.0.0";
            o.HandleShutdownSignals = false;
        });
        if (!init.Success)
            throw new InvalidOperationException("CodeLogic init failed: " + init.Message);

        var configDir = Path.Combine(root, "Libraries", "CL.MySQL2");
        Directory.CreateDirectory(configDir);
        File.WriteAllText(Path.Combine(configDir, "config.mysql.json"), $$"""
            {
              "databases": {
                "Default": {
                  "enabled": true,
                  "host": "{{Env("WEBLOGIC_MYSQL_TEST_HOST", "127.0.0.1")}}",
                  "port": {{Env("WEBLOGIC_MYSQL_TEST_PORT", "3306")}},
                  "database": "{{Env("WEBLOGIC_MYSQL_TEST_DB", "wl_test")}}",
                  "username": "{{Env("WEBLOGIC_MYSQL_TEST_USER", "root")}}",
                  "password": "{{Env("WEBLOGIC_MYSQL_TEST_PASS", "")}}",
                  "syncMode": "developer",
                  "characterSet": "utf8mb4"
                }
              }
            }
            """);
        await Libraries.LoadAsync<MySQL2Library>();
        await CodeLogic.CodeLogic.ConfigureAsync();
        await CodeLogic.CodeLogic.StartAsync();

        Db = new WebMySqlDatabase(new CodeLogicLibraryResolver(), new WebMySqlOptions());
        // Every run starts from freshly created tables.
        await DropAsync<UserRow>();
        await DropAsync<UserLoginRow>();
        await DropAsync<SessionRow>();
        await DropAsync<ApiKeyRow>();
        await DropAsync<RecoveryCodeRow>();
        await DropAsync<OneTimeUseRow>();
        await DropAsync<LoginThrottleRow>();
        await DropAsync<RateLimitRow>();
        await DropAsync<AuditRow>();
        await DropAsync<WidgetSettingsRow>();
        await DropAsync<DashboardRow>();
        await DropAsync<DataProtectionKeyRow>();
        await DropAsync<PasskeyRow>();
        WebMySqlDatabase.Value(await Db.MySql.SyncSchemaAsync(AllTables, false, "Default", default));
    }

    /// <summary>Deletes every row of <typeparamref name="T"/>'s table.</summary>
    public async Task ClearAsync<T>() where T : class, new() =>
        WebMySqlDatabase.Value(await Db.MySql.TruncateTableAsync<T>("Default"));

    private async Task DropAsync<T>() where T : class =>
        WebMySqlDatabase.Value(await Db.MySql.DropTableAsync<T>(true, "Default"));

    public async Task DisposeAsync()
    {
        if (Configured)
            await CodeLogic.CodeLogic.StopAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class MySqlTestGroup : ICollectionFixture<MySqlFixture>
{
    public const string Name = "mysql";
}

using CL.Data.Hosting;
using CL.Data.MySql;
using CL.WebLogic;
using CL.WebLogic.Hosting.Services;
using CodeLogic.Framework.Application.Plugins;
using Microsoft.AspNetCore.DataProtection;
using MiniBlog.Application;
using Samples.Hosting;
using CL.WebLogic.Data.Hosting;

// Container health check: `dotnet MiniBlog.dll --probe`.
if (HealthProbe.IsProbe(args))
    return await HealthProbe.RunAsync(args);

// ── 1. Environment → CodeLogic config files ─────────────────────────────────
var paths = SamplePaths.FromEnvironment();
MiniBlogSetup.WriteConfig(paths);

// ── 2. CodeLogic: libraries, application, then plugins ──────────────────────
var init = await CodeLogic.CodeLogic.InitializeAsync(o =>
{
    o.FrameworkRootPath = paths.FrameworkRoot;
    o.ApplicationRootPath = paths.ApplicationRoot;
    o.AppVersion = "5.0.0";
    o.HandleShutdownSignals = false;
});
if (!init.Success || init.ShouldExit)
{
    await Console.Error.WriteLineAsync($"CodeLogic init failed: {init.Message}");
    return 1;
}

await WebLogicBootstrap.LoadAsync();
await CodeLogic.Libraries.LoadAsync<DataLibrary>();
await CodeLogic.Libraries.LoadAsync<MySqlDataLibrary>();
if (SampleConfig.HasSmtp)
    await CodeLogic.Libraries.LoadAsync<CL.Mail.MailLibrary>();
var application = new MiniBlogApplication();
CodeLogic.CodeLogic.RegisterApplication(application);
await CodeLogic.CodeLogic.ConfigureAsync();
await CodeLogic.CodeLogic.StartAsync();

// Plugins run from the data volume (CodeLogic keeps their config and logs next to them);
// the image ships them read-only under plugins/.
var pluginOptions = new PluginOptions
{
    PluginsDirectory = PluginInstaller.Install(Path.Combine(AppContext.BaseDirectory, "plugins"), paths.Folder("plugins")),
    EnableHotReload = false,
    WatchForChanges = false
};
// Types from these cross the host/plugin boundary (IWebContributor, BlogPostService, CL.Data rows):
// the plugin always gets the host's copy, even if it ships one (CodeLogic 5 isolates plugins).
foreach (var shared in new[] { "CL.WebLogic", "MiniBlog.Data", "CL.Data" })
    pluginOptions.SharedAssemblies.Add(shared);
var plugins = new PluginManager(CodeLogic.CodeLogic.GetEventBus(), pluginOptions);
CodeLogic.CodeLogic.SetPluginManager(plugins);
await plugins.LoadAllAsync();

// ── 3. The web host (WebLogic registers the loaded plugins' routes and widgets) ──
var builder = WebApplication.CreateBuilder(args);
// The key ring lives in the database with the rest of the state, so instances share it; it is encrypted
// at rest when MINIBLOG_KEYRING_SECRET is set (32+ characters).
var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName("MiniBlog")
    .PersistKeysToDatabase(application.Config.ConnectionName);
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MINIBLOG_KEYRING_SECRET")))
    dataProtection.ProtectKeysWithSecret("MINIBLOG_KEYRING_SECRET");
builder.AddCodeLogicWebLogic(web => MiniBlogSetup.ConfigureWebLogic(web, application.Config));

var app = builder.Build();
app.UseCodeLogicWebLogic();
app.MapCodeLogicWebLogic();
await app.RunAsync();
return 0;

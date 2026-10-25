using CL.WebLogic;
using CL.WebLogic.Hosting.Services;
using Microsoft.AspNetCore.DataProtection;
using Samples.Hosting;
using StarterWebsite.Application;

// Container health check: `dotnet StarterWebsite.dll --probe`.
if (HealthProbe.IsProbe(args))
    return await HealthProbe.RunAsync(args);

// ── 1. Environment → CodeLogic config files ─────────────────────────────────
var paths = SamplePaths.FromEnvironment();
StarterSetup.WriteConfig(paths);

// ── 2. Boot CodeLogic (libraries + application) before the web host ─────────
var init = await CodeLogic.CodeLogic.InitializeAsync(o =>
{
    o.FrameworkRootPath = paths.FrameworkRoot;
    o.ApplicationRootPath = paths.ApplicationRoot;
    o.AppVersion = "4.9.0";
    o.HandleShutdownSignals = false; // ASP.NET Core owns Ctrl+C / SIGTERM
});
if (!init.Success || init.ShouldExit)
{
    await Console.Error.WriteLineAsync($"CodeLogic init failed: {init.Message}");
    return 1;
}

await WebLogicBootstrap.LoadAsync();
if (SampleConfig.HasSmtp)
    await CodeLogic.Libraries.LoadAsync<CL.Mail.MailLibrary>(); // security e-mails over SMTP
var application = new StarterApplication();
CodeLogic.CodeLogic.RegisterApplication(application);
await CodeLogic.CodeLogic.ConfigureAsync();
await CodeLogic.CodeLogic.StartAsync();

// ── 3. The web host ──────────────────────────────────────────────────────────
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDataProtection()
    .SetApplicationName("StarterWebsite")
    .PersistKeysToFileSystem(new DirectoryInfo(paths.KeysFolder));
builder.AddCodeLogicWebLogic(web => StarterSetup.ConfigureWebLogic(web, application.Config, paths));

var app = builder.Build();
app.UseCodeLogicWebLogic();
app.MapCodeLogicWebLogic();
await app.RunAsync();
return 0;

using CL.WebLogic.Auth.Events;
using CL.WebLogic.Forms.Models;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Realtime.Models;
using CL.WebLogic.Routing.Models;
using Samples.Hosting;
using StarterWebsite.Configuration;
using StarterWebsite.Forms;
using StarterWebsite.Infrastructure;
using StarterWebsite.Pages;
using CL.WebLogic.ExternalLogins.Hosting;
using StarterWebsite.Widgets;

namespace StarterWebsite.Application;

/// <summary>Everything the starter configures: config files from the environment, stores, forms and pages.</summary>
public static class StarterSetup
{
    /// <summary>Policy for <c>/api/v1</c>: an API key with the <c>profile:read</c> scope.</summary>
    public const string ApiPolicy = "starter-api";

    /// <summary>Writes the environment into the CodeLogic config files (see samples/docker/.env.example).</summary>
    public static void WriteConfig(SamplePaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        SampleConfig.WebLogic(paths)
            .Default("siteName", "WebLogic Starter")
            .Default("security.loginPath", "/account/login")
            .Default("auth.twoFactor.issuer", "WebLogic Starter")
            .Default("realtime.clientAutoConnect", true)
            .Default("client.uiAdapter", "bootstrap5")
            .Save();
        SampleConfig.Mail(paths).Save();
        ConfigFilePatch.Open(paths.ApplicationConfig())
            .Set("steamSignIn", DemoEnvironment.Flag("STEAM_SIGNIN"))
            .Set("allowRegistration", DemoEnvironment.Flag("DEMO_ALLOW_REGISTRATION"))
            .Save();
        foreach (var (library, section) in new[] { ("CL.Storage", "storage"), ("CL.GitHelper", "githelper"), ("CL.NetUtils", "netutils") })
            SampleConfig.DisableUnlessConfigured(paths, library, section);
    }

    /// <summary>Registers the starter's stores, forms, pages and widgets with WebLogic.</summary>
    public static void ConfigureWebLogic(WebLogicBuilder web, StarterConfig config, SamplePaths paths)
    {
        ArgumentNullException.ThrowIfNull(web);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(paths);

        web.StopCodeLogicOnShutdown();
        web.Services.AddSingleton(config);
        web.Services.AddSingleton(new StarterDataFolder(paths.Folder("starter")));
        web.Services.AddSingleton<ShoutWall>();
        web.Services.AddHostedService<AdminAccountSeeder>();

        // App-owned persistence: JSON files on the data volume. The toolkit only defines the contracts.
        web.UseUserStore<JsonUserStore>()
           .UseSessionStore<JsonSessionStore>()
           .UseApiKeyStore<JsonApiKeyStore>()
           .UseRecoveryCodeStore<JsonRecoveryCodeStore>()
           .UsePasskeyStore<JsonPasskeyStore>()
           .UseDashboardStore<JsonDashboardLayoutStore>()
           .UseWidgetSettingsStore<JsonWidgetSettingsStore>()
           .UseDashboardDefaults<StarterDashboardDefaults>();
        if (SampleConfig.HasSmtp)
            web.UseMailNotifications();
        else
            web.UseOutboxNotifications();

        // External sign-in (CodeLogic.WebLogic.ExternalLogins): Steam needs no keys; the OAuth
        // providers turn on when STARTER_{GOOGLE|MICROSOFT|GITHUB|DISCORD}_CLIENT_ID and _SECRET are set.
        if (config.SteamSignIn)
            web.AddSteamLogin();
        foreach (var (provider, add) in new (string, Func<string, string, WebLogicBuilder>)[]
        {
            ("GOOGLE", (id, secret) => web.AddGoogleLogin(id, secret)),
            ("MICROSOFT", (id, secret) => web.AddMicrosoftLogin(id, secret)),
            ("GITHUB", (id, secret) => web.AddGitHubLogin(id, secret)),
            ("DISCORD", (id, secret) => web.AddDiscordLogin(id, secret))
        })
        {
            if (Environment.GetEnvironmentVariable($"STARTER_{provider}_CLIENT_ID") is { Length: > 0 } clientId
                && Environment.GetEnvironmentVariable($"STARTER_{provider}_CLIENT_SECRET") is { Length: > 0 } clientSecret)
                add(clientId, clientSecret);
        }

        web.AddAuthorizationPolicy(ApiPolicy, p => p.RequireScheme(CL.WebLogic.Security.Identity.WebAuthenticationSchemes.ApiKey).RequireScopes("profile:read"));
        web.AddOutputCachePolicy(CachePages.Policy, p => p.Tag(CachePages.Tag).Expire(TimeSpan.FromMinutes(10)));
        web.BridgeEvent<UserRegisteredEvent>("starter.member-joined", _ => WebRealtimeAudience.Public, _ => new { message = "A new member just joined." });

        AddForms(web);
        AddContributor<HomePages>(web, "starter.home", "Home");
        AddContributor<AccountPages>(web, "starter.account", "Accounts");
        AddContributor<FormPages>(web, "starter.forms", "Forms gallery");
        AddContributor<DashboardPages>(web, "starter.dashboard", "Dashboard");
        AddContributor<RealtimePages>(web, "starter.realtime", "Live wall");
        AddContributor<CachePages>(web, "starter.cache", "Caching");
        AddContributor<ApiEndpoints>(web, "starter.api", "API v1");
        AddContributor<StarterWidgets>(web, "starter.widgets", "Widgets");
    }

    private static void AddForms(WebLogicBuilder web)
    {
        web.AddForm<LoginForm>()
           .AddForm<RegisterForm>()
           .AddForm<TwoFactorForm>()
           .AddForm<RecoveryCodeForm>()
           .AddForm<ForgotPasswordForm>()
           .AddForm<ResetPasswordForm>()
           .AddForm<ChangePasswordForm>()
           .AddForm<TwoFactorSetupForm>()
           .AddForm<ApiKeyForm>()
           .AddForm<EventSignupForm>()
           .AddForm<ProjectWizardForm>()
           .AddFormValidator<RegisterForm, RegisterFormValidator>()
           .AddFormValidator<EventSignupForm, EventSignupValidator>()
           .AddFormOptions("countries", [new WebFieldOption("dk", "Denmark"), new WebFieldOption("se", "Sweden"), new WebFieldOption("no", "Norway"), new WebFieldOption("de", "Germany"), new WebFieldOption("other", "Somewhere else")])
           .AddFormOptions("tracks", [new WebFieldOption("web", "Web"), new WebFieldOption("security", "Security"), new WebFieldOption("data", "Data"), new WebFieldOption("ops", "Operations")])
           .AddFormOptions("api-scopes", [new WebFieldOption("profile:read", "Read my profile"), new WebFieldOption("wall:write", "Post to the live wall")], allowAnonymous: false);
    }

    private static void AddContributor<T>(WebLogicBuilder web, string id, string name) where T : class, CL.WebLogic.Routing.IWebContributor
    {
        web.Services.AddSingleton<T>();
        web.AddContributor<T>(new WebContributorDescriptor { Id = id, Name = name, Kind = "Application" });
    }
}

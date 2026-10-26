using CL.WebLogic.Auth.ApiKeys;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.MySql.Hosting;
using CL.WebLogic.MySql.Models;
using CL.WebLogic.Routing.Models;
using MiniBlog.Configuration;
using MiniBlog.Data;
using MiniBlog.Data.Posts;
using MiniBlog.Data.Stores;
using MiniBlog.Forms;
using MiniBlog.Pages;
using Samples.Hosting;

namespace MiniBlog.Application;

/// <summary>Config files from the environment, MySQL stores, forms and the public pages.</summary>
public static class MiniBlogSetup
{
    /// <summary>
    /// Writes the environment into the config files: the WebLogic/mail variables every sample reads,
    /// plus <c>MINIBLOG_MYSQL_HOST</c>, <c>_PORT</c>, <c>_DATABASE</c>, <c>_USERNAME</c>, <c>_PASSWORD</c>.
    /// </summary>
    public static void WriteConfig(SamplePaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        SampleConfig.WebLogic(paths)
            .Default("siteName", "Northwind Journal")
            .Default("forms.renderer", "plain")
            .Default("client.uiAdapter", "plain")
            .Save();
        SampleConfig.Mail(paths).Save();
        var host = DemoEnvironment.Text("MINIBLOG_MYSQL_HOST");
        ConfigFilePatch.Open(paths.LibraryConfig("CL.MySQL2", "mysql"))
            .Set("databases.Default.host", host)
            .Set("databases.Default.port", DemoEnvironment.Number("MINIBLOG_MYSQL_PORT"))
            .Set("databases.Default.database", DemoEnvironment.Text("MINIBLOG_MYSQL_DATABASE"))
            .Set("databases.Default.username", DemoEnvironment.Text("MINIBLOG_MYSQL_USERNAME"))
            .Set("databases.Default.password", DemoEnvironment.Text("MINIBLOG_MYSQL_PASSWORD"))
            // MySQL 8 hands out its RSA key for caching_sha2_password over the private network.
            .Set("databases.Default.allowPublicKeyRetrieval", host is null ? null : true)
            .Save();
        foreach (var (library, section) in new[] { ("CL.Storage", "storage"), ("CL.GitHelper", "githelper"), ("CL.NetUtils", "netutils") })
            SampleConfig.DisableUnlessConfigured(paths, library, section);
    }

    /// <summary>Registers the blog's stores, forms and pages.</summary>
    public static void ConfigureWebLogic(WebLogicBuilder web, MiniBlogConfig config)
    {
        ArgumentNullException.ThrowIfNull(web);
        ArgumentNullException.ThrowIfNull(config);

        web.StopCodeLogicOnShutdown();
        web.Services.AddSingleton(config);
        web.Services.AddSingleton(new BlogDatabase(config.ConnectionId));
        web.Services.AddSingleton<DocumentStore>();
        web.Services.AddSingleton<BlogPostService>();

        // Every WebLogic store on MySQL (CodeLogic.WebLogic.MySql), so several instances can share
        // the database. The blog has no API, so API keys stay switched off.
        web.UseMySqlStores(config.ConnectionId, o => o.Stores = WebMySqlStores.All & ~WebMySqlStores.ApiKeys)
           .UseApiKeyStore<NullWebApiKeyStore>();
        // After UseMySqlStores: hosted services start in order, and the seeder needs the wl_* tables.
        web.Services.AddHostedService<BlogSeeder>();
        if (SampleConfig.HasSmtp)
            web.UseMailNotifications();
        else
            web.UseOutboxNotifications();

        web.AddOutputCachePolicy(BlogCache.Policy, p => p.Tag(BlogCache.Tag).Expire(TimeSpan.FromMinutes(30)));
        web.AddForm<LoginForm>()
           .AddForm<ForgotPasswordForm>()
           .AddForm<ResetPasswordForm>();

        web.Services.AddSingleton<BlogPages>();
        web.AddContributor<BlogPages>(new WebContributorDescriptor { Id = "miniblog.pages", Name = "Blog", Kind = "Application" });
        web.Services.AddSingleton<AccountPages>();
        web.AddContributor<AccountPages>(new WebContributorDescriptor { Id = "miniblog.account", Name = "Sign-in", Kind = "Application" });
    }
}

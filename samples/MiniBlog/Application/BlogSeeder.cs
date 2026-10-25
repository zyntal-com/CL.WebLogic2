using System.Security.Cryptography;
using CL.WebLogic.Auth.Accounts;
using CL.WebLogic.Auth.Users;
using CL.WebLogic.Hosting.Services;
using MiniBlog.Configuration;
using MiniBlog.Data;
using MiniBlog.Data.Posts;
using MiniBlog.Data.Stores;
using Samples.Hosting;

namespace MiniBlog.Application;

/// <summary>
/// Startup data work: syncs the schema, creates the editor account once and seeds the demo posts
/// once (a marker document records it, so restarts never overwrite edits or resurrect deletions).
/// </summary>
public sealed class BlogSeeder : IHostedService
{
    private readonly BlogDatabase _db;
    private readonly DocumentStore _documents;
    private readonly BlogPostService _posts;
    private readonly WebAccountService _accounts;
    private readonly IWebUserStore _users;
    private readonly MiniBlogConfig _config;
    private readonly IWebLogicContext _context;

    /// <summary>Creates the seeder.</summary>
    public BlogSeeder(BlogDatabase db, DocumentStore documents, BlogPostService posts, WebAccountService accounts, IWebUserStore users, MiniBlogConfig config, IWebLogicContext context)
    {
        _db = db;
        _documents = documents;
        _posts = posts;
        _accounts = accounts;
        _users = users;
        _config = config;
        _context = context;
    }

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _db.SyncSchemaAsync(cancellationToken).ConfigureAwait(false);
        await EnsureEditorAsync(cancellationToken).ConfigureAwait(false);

        var (seeded, _) = await _documents.GetAsync<string>("seed", DemoContent.SeedVersion, cancellationToken).ConfigureAwait(false);
        if (seeded is not null)
            return;
        foreach (var post in DemoContent.Posts)
            await _posts.CreateAsync(post, cancellationToken).ConfigureAwait(false);
        await _documents.SetAsync("seed", DemoContent.SeedVersion, DateTimeOffset.UtcNow.ToString("O"), cancellationToken).ConfigureAwait(false);
        _context.Logger.Info($"Seeded {DemoContent.Posts.Count} demo posts");
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task EnsureEditorAsync(CancellationToken cancellationToken)
    {
        if (await _users.FindByLoginAsync(_config.AdminUserName, cancellationToken).ConfigureAwait(false) is not null)
            return;
        var configured = DemoEnvironment.Text("DEMO_ADMIN_PASSWORD");
        var password = configured ?? Convert.ToBase64String(RandomNumberGenerator.GetBytes(15)).Replace('+', 'x').Replace('/', 'y');
        var created = await _accounts.RegisterAsync(new WebRegistration
        {
            UserName = _config.AdminUserName,
            Email = _config.AdminEmail,
            Password = password,
            Groups = ["admin"],
            Permissions = ["weblogic.explorer", "weblogic.internal"],
            EmailConfirmed = true,
            SendVerificationEmail = false
        }, cancellationToken).ConfigureAwait(false);
        if (created.IsFailure)
            _context.Logger.Error($"Could not create the editor account: {created.Error!.Message}");
        else if (configured is null)
            _context.Logger.Warning($"Created editor account '{_config.AdminUserName}' with the one-time password: {password}  (set DEMO_ADMIN_PASSWORD to choose it)");
    }
}

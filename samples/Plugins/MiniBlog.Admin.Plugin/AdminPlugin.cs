using CL.WebLogic.Caching.Services;
using CL.WebLogic.Forms.Services;
using CL.WebLogic.Results;
using CL.WebLogic.Routing;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Runtime.Models;
using CL.WebLogic.Security.Authorization;
using CL.WebLogic.Theming.Services;
using CL.WebLogic.Widgets.Models;
using CL.WebLogic.Widgets.Services;
using CodeLogic.Framework.Application.Plugins;
using CodeLogic.Framework.Libraries;
using Microsoft.Extensions.DependencyInjection;
using MiniBlog.Data.Posts;

namespace MiniBlog.Admin;

/// <summary>
/// The admin area, loaded from <c>plugins/MiniBlog.Admin.Plugin</c>. WebLogic registers its
/// routes, form and widget when the plugin loads and removes them all when it unloads — the
/// host knows nothing about it. Every route requires the <c>admin</c> group; writes evict the
/// public pages from the output cache.
/// <list type="number">
///   <item>Configure / Initialize / Start — nothing to set up</item>
///   <item>ContributeAsync — its templates (embedded <c>theme/</c>), routes under <c>/admin</c>, the <c>post-editor</c> form, the <c>blog-stats</c> widget</item>
/// </list>
/// </summary>
public sealed class AdminPlugin : IPlugin, IWebContributor
{
    private const string EditorTemplate = "admin/editor";

    /// <inheritdoc/>
    public PluginManifest Manifest { get; } = new()
    {
        Id = "miniblog.admin",
        Name = "MiniBlog Admin",
        Version = "4.9.0",
        Description = "Post management for MiniBlog",
        Author = "Zyntal.com"
    };

    /// <inheritdoc/>
    public PluginState State { get; private set; } = PluginState.Loaded;

    // ── Plugin lifecycle ──────────────────────────────────────────────────────

    /// <inheritdoc/>
    public Task OnConfigureAsync(PluginContext context)
    {
        State = PluginState.Configured;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task OnInitializeAsync(PluginContext context)
    {
        State = PluginState.Initialized;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task OnStartAsync(PluginContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Logger.Info($"{Manifest.Name} started");
        State = PluginState.Started;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task OnUnloadAsync()
    {
        State = PluginState.Stopped;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<HealthStatus> HealthCheckAsync() => Task.FromResult(HealthStatus.Healthy($"{Manifest.Name} is running"));

    /// <inheritdoc/>
    public void Dispose()
    {
    }

    // ── Web contributions ─────────────────────────────────────────────────────

    /// <inheritdoc/>
    public Task ContributeAsync(WebContributionBuilder builder, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var posts = builder.Services.GetRequiredService<BlogPostService>();
        var cache = builder.Services.GetRequiredService<IWebCacheInvalidator>();
        var handlers = new AdminHandlers(posts, cache);

        builder.MapTemplates(typeof(AdminPlugin).Assembly);
        builder.MapForm<PostEditorForm>();
        var admin = builder.MapGroup("/admin").RequireGroups("admin");
        admin.MapPage("", handlers.IndexAsync).WithName("admin");
        admin.MapPage("/posts/new", AdminHandlers.NewAsync);
        admin.MapPage("/posts/new", handlers.CreateAsync, "POST");
        admin.MapPage("/posts/{id}", handlers.EditAsync);
        admin.MapPage("/posts/{id}", handlers.UpdateAsync, "POST");
        admin.MapPage("/posts/{id}/delete", handlers.DeleteAsync, "POST");

        builder.MapWidget("blog-stats", async (_, ct) =>
        {
            var (published, drafts) = await posts.CountsAsync(ct).ConfigureAwait(false);
            return WebWidgetResult.Template("widgets/blog-stats", new { Published = published, Drafts = drafts });
        }, o =>
        {
            o.Access = new WebAccessRule { Groups = ["admin"] };
            o.Description = "Post counts (editors only)";
            o.Dashboard = false;
        });
        builder.PlaceWidget("sidebar", "blog-stats", o => o.ExcludePaths.Add("/admin/**"));
        return Task.CompletedTask;
    }

    /// <summary>The admin route handlers.</summary>
    private sealed class AdminHandlers(BlogPostService posts, IWebCacheInvalidator cache)
    {
        public async ValueTask<WebResult> IndexAsync(WebRequestContext request, CancellationToken cancellationToken)
        {
            var all = await posts.AllAsync(cancellationToken).ConfigureAwait(false);
            return WebResult.Page("admin/index", new
            {
                Posts = all.Select(p => new { p.Id, p.Title, p.Url, p.Published, p.UpdatedAt, Tags = string.Join(", ", p.Tags) }).ToList(),
                Saved = request.GetQuery("saved") == "1",
                Deleted = request.GetQuery("deleted") == "1"
            }, new WebPageMeta { Title = "Admin", Robots = "noindex" });
        }

        public static ValueTask<WebResult> NewAsync(WebRequestContext request, CancellationToken cancellationToken) =>
            ValueTask.FromResult<WebResult>(WebResult.Page(EditorTemplate, new { Heading = "New post", Action = "/admin/posts/new" }, new WebPageMeta { Title = "New post", Robots = "noindex" }));

        public async ValueTask<WebResult> CreateAsync(WebRequestContext request, CancellationToken cancellationToken)
        {
            var submission = await request.Forms.BindAsync<PostEditorForm>(cancellationToken).ConfigureAwait(false);
            if (!submission.IsValid)
                return submission.Invalid(fallbackTemplate: EditorTemplate, fallbackModel: new { Heading = "New post", Action = "/admin/posts/new" });
            await posts.CreateAsync(submission.Model.ToDraft(request.Identity.UserName ?? "editor"), cancellationToken).ConfigureAwait(false);
            await cache.EvictTagsAsync([BlogCache.Tag], cancellationToken).ConfigureAwait(false);
            return FormResult.Success("Post created.", "/admin?saved=1");
        }

        public async ValueTask<WebResult> EditAsync(WebRequestContext request, CancellationToken cancellationToken)
        {
            var id = request.GetRouteValue("id") ?? string.Empty;
            var post = await posts.FindAsync(id, cancellationToken).ConfigureAwait(false);
            if (post is null)
                return WebResult.Status(404);
            request.Forms.Prefill(PostEditorForm.From(post));
            return WebResult.Page(EditorTemplate, new { Heading = "Edit post", Action = "/admin/posts/" + Uri.EscapeDataString(id), post.Url, post.Id },
                new WebPageMeta { Title = "Edit " + post.Title, Robots = "noindex" });
        }

        public async ValueTask<WebResult> UpdateAsync(WebRequestContext request, CancellationToken cancellationToken)
        {
            var id = request.GetRouteValue("id") ?? string.Empty;
            var submission = await request.Forms.BindAsync<PostEditorForm>(cancellationToken).ConfigureAwait(false);
            if (!submission.IsValid)
                return submission.Invalid(fallbackTemplate: EditorTemplate, fallbackModel: new { Heading = "Edit post", Action = "/admin/posts/" + Uri.EscapeDataString(id) });
            var updated = await posts.UpdateAsync(id, submission.Model.ToDraft(request.Identity.UserName ?? "editor"), cancellationToken).ConfigureAwait(false);
            if (updated is null)
                return WebResult.Status(404);
            await cache.EvictTagsAsync([BlogCache.Tag], cancellationToken).ConfigureAwait(false);
            return FormResult.Success("Post saved.", "/admin?saved=1");
        }

        public async ValueTask<WebResult> DeleteAsync(WebRequestContext request, CancellationToken cancellationToken)
        {
            await posts.DeleteAsync(request.GetRouteValue("id") ?? string.Empty, cancellationToken).ConfigureAwait(false);
            await cache.EvictTagsAsync([BlogCache.Tag], cancellationToken).ConfigureAwait(false);
            return WebResult.Redirect("/admin?deleted=1");
        }
    }
}

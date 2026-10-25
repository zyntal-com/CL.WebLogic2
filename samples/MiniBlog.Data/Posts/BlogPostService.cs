using System.Security.Cryptography;
using CL.MySQL2.Models;
using MiniBlog.Data.Entities;

namespace MiniBlog.Data.Posts;

/// <summary>Reading and writing posts with CL.MySQL2 (typed queries, server-side paging).</summary>
public sealed class BlogPostService
{
    private readonly BlogDatabase _db;

    /// <summary>Creates the service.</summary>
    public BlogPostService(BlogDatabase db) => _db = db;

    /// <summary>A page of published posts, newest first (optionally with a tag).</summary>
    public async Task<PagedResult<BlogPost>> PublishedAsync(int page, int pageSize, string? tag, CancellationToken cancellationToken)
    {
        var query = _db.MySql.Query<PostEntity>(_db.ConnectionId).Where(p => p.Status == "published");
        if (!string.IsNullOrWhiteSpace(tag))
        {
            var needle = "," + tag.Trim().ToLowerInvariant() + ",";
            query = query.Where(p => p.Tags.Contains(needle));
        }
        var result = await query.OrderByDescending(p => p.PublishedUtc).ToPagedListAsync(Math.Max(1, page), pageSize, cancellationToken).ConfigureAwait(false);
        var paged = Unwrap(result);
        return new PagedResult<BlogPost> { Items = [.. paged.Items.Select(ToPost)], PageNumber = paged.PageNumber, PageSize = paged.PageSize, TotalItems = paged.TotalItems };
    }

    /// <summary>Every post (drafts included) for the admin list, newest change first.</summary>
    public async Task<IReadOnlyList<BlogPost>> AllAsync(CancellationToken cancellationToken) =>
        [.. Unwrap(await _db.MySql.Query<PostEntity>(_db.ConnectionId).OrderByDescending(p => p.UpdatedUtc).Take(500).ToListAsync(cancellationToken).ConfigureAwait(false)).Select(ToPost)];

    /// <summary>A published post by slug.</summary>
    public async Task<BlogPost?> FindPublishedAsync(string slug, CancellationToken cancellationToken)
    {
        var rows = Unwrap(await _db.MySql.Query<PostEntity>(_db.ConnectionId).Where(p => p.Slug == slug && p.Status == "published").Take(1).ToListAsync(cancellationToken).ConfigureAwait(false));
        return rows.Count == 0 ? null : ToPost(rows[0]);
    }

    /// <summary>Any post by id.</summary>
    public async Task<BlogPost?> FindAsync(string id, CancellationToken cancellationToken) =>
        Unwrap(await _db.MySql.GetRepository<PostEntity>(_db.ConnectionId).GetByIdAsync(id, cancellationToken).ConfigureAwait(false)) is { } entity ? ToPost(entity) : null;

    /// <summary>Creates a post with a unique slug.</summary>
    public async Task<BlogPost> CreateAsync(BlogPostDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var entity = new PostEntity { Id = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(12)), Slug = await UniqueSlugAsync(BlogSlug.From(draft.Title), cancellationToken).ConfigureAwait(false) };
        Apply(entity, draft);
        return ToPost(Unwrap(await _db.MySql.GetRepository<PostEntity>(_db.ConnectionId).InsertAsync(entity, cancellationToken).ConfigureAwait(false)));
    }

    /// <summary>Updates a post (the slug stays, so links keep working).</summary>
    public async Task<BlogPost?> UpdateAsync(string id, BlogPostDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var repository = _db.MySql.GetRepository<PostEntity>(_db.ConnectionId);
        var entity = Unwrap(await repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false));
        if (entity is null)
            return null;
        Apply(entity, draft);
        return ToPost(Unwrap(await repository.UpdateAsync(entity, cancellationToken).ConfigureAwait(false)));
    }

    /// <summary>Deletes a post.</summary>
    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken) =>
        Unwrap(await _db.MySql.GetRepository<PostEntity>(_db.ConnectionId).DeleteAsync(id, cancellationToken).ConfigureAwait(false));

    /// <summary>Number of posts by status.</summary>
    public async Task<(long Published, long Drafts)> CountsAsync(CancellationToken cancellationToken)
    {
        var published = Unwrap(await _db.MySql.Query<PostEntity>(_db.ConnectionId).Where(p => p.Status == "published").CountAsync(cancellationToken).ConfigureAwait(false));
        var drafts = Unwrap(await _db.MySql.Query<PostEntity>(_db.ConnectionId).Where(p => p.Status != "published").CountAsync(cancellationToken).ConfigureAwait(false));
        return (published, drafts);
    }

    private static void Apply(PostEntity entity, BlogPostDraft draft)
    {
        entity.Title = draft.Title.Trim();
        entity.BodyMarkdown = draft.Body;
        entity.Summary = Summarize(draft.Body);
        entity.Tags = "," + string.Join(',', draft.Tags.Select(t => t.Trim().ToLowerInvariant()).Where(t => t.Length > 0).Distinct()) + ",";
        entity.Author = draft.Author;
        entity.Status = draft.Published ? "published" : "draft";
        entity.UpdatedUtc = DateTime.UtcNow;
        if (draft.Published && entity.PublishedUtc is null)
            entity.PublishedUtc = DateTime.UtcNow;
    }

    private async Task<string> UniqueSlugAsync(string slug, CancellationToken cancellationToken)
    {
        for (var n = 1; ; n++)
        {
            var candidate = n == 1 ? slug : $"{slug}-{n}";
            var taken = Unwrap(await _db.MySql.Query<PostEntity>(_db.ConnectionId).Where(p => p.Slug == candidate).CountAsync(cancellationToken).ConfigureAwait(false));
            if (taken == 0)
                return candidate;
        }
    }

    private static string Summarize(string markdown)
    {
        var text = string.Join(' ', markdown.Split('\n')
            .Select(l => l.Trim().TrimStart('>', '-', '*', ' '))
            .Where(l => l.Length > 0 && !l.StartsWith('#') && !l.StartsWith('<') && !l.StartsWith("```", StringComparison.Ordinal)));
        text = text.Replace("**", string.Empty, StringComparison.Ordinal).Replace("`", string.Empty, StringComparison.Ordinal);
        return text.Length <= 220 ? text : text[..217].TrimEnd() + "…";
    }

    private static BlogPost ToPost(PostEntity e) => new()
    {
        Id = e.Id,
        Slug = e.Slug,
        Title = e.Title,
        Summary = e.Summary,
        Body = e.BodyMarkdown,
        Tags = e.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries),
        Published = e.Status == "published",
        Author = e.Author,
        PublishedAt = e.PublishedUtc is { } p ? new DateTimeOffset(DateTime.SpecifyKind(p, DateTimeKind.Utc)) : null,
        UpdatedAt = new DateTimeOffset(DateTime.SpecifyKind(e.UpdatedUtc, DateTimeKind.Utc))
    };

    private static T Unwrap<T>(CodeLogic.Core.Results.Result<T> result) =>
        result.IsSuccess ? result.Value! : throw new InvalidOperationException("MiniBlog posts: " + result.Error!.Message);
}

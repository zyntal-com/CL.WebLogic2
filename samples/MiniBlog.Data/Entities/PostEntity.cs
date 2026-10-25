using CL.MySQL2.Models;

namespace MiniBlog.Data.Entities;

/// <summary>A blog post. The body is Markdown, rendered (and encoded) by the theme.</summary>
[Table(Name = "miniblog_posts")]
[CompositeIndex("ix_miniblog_posts_status_published", "status", "published_utc")]
public sealed class PostEntity
{
    /// <summary>Id.</summary>
    [Column(Name = "id", DataType = DataType.VarChar, Size = 32, Primary = true, NotNull = true)]
    public string Id { get; set; } = string.Empty;

    /// <summary>URL slug.</summary>
    [Column(Name = "slug", DataType = DataType.VarChar, Size = 180, NotNull = true, Unique = true)]
    public string Slug { get; set; } = string.Empty;

    /// <summary>Title.</summary>
    [Column(Name = "title", DataType = DataType.VarChar, Size = 180, NotNull = true)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Teaser.</summary>
    [Column(Name = "summary", DataType = DataType.VarChar, Size = 400, NotNull = true)]
    public string Summary { get; set; } = string.Empty;

    /// <summary>Markdown body.</summary>
    [Column(Name = "body_markdown", DataType = DataType.LongText, NotNull = true)]
    public string BodyMarkdown { get; set; } = string.Empty;

    /// <summary>Tags as <c>,a,b,</c> so a LIKE '%,tag,%' finds them.</summary>
    [Column(Name = "tags", DataType = DataType.VarChar, Size = 400, NotNull = true)]
    public string Tags { get; set; } = ",";

    /// <summary>draft or published.</summary>
    [Column(Name = "status", DataType = DataType.VarChar, Size = 16, NotNull = true, Index = true)]
    public string Status { get; set; } = "draft";

    /// <summary>Author's display name.</summary>
    [Column(Name = "author", DataType = DataType.VarChar, Size = 120, NotNull = true)]
    public string Author { get; set; } = string.Empty;

    /// <summary>First publication (UTC).</summary>
    [Column(Name = "published_utc", DataType = DataType.DateTime)]
    public DateTime? PublishedUtc { get; set; }

    /// <summary>Last change (UTC).</summary>
    [Column(Name = "updated_utc", DataType = DataType.DateTime, NotNull = true)]
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}

namespace MiniBlog.Data.Posts;

/// <summary>A post as pages and the admin plugin see it.</summary>
public sealed record BlogPost
{
    /// <summary>Id.</summary>
    public required string Id { get; init; }

    /// <summary>URL slug.</summary>
    public required string Slug { get; init; }

    /// <summary>Title.</summary>
    public required string Title { get; init; }

    /// <summary>Teaser.</summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>Markdown body.</summary>
    public string Body { get; init; } = string.Empty;

    /// <summary>Tags.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Published (false = draft).</summary>
    public bool Published { get; init; }

    /// <summary>Author's display name.</summary>
    public string Author { get; init; } = string.Empty;

    /// <summary>First publication.</summary>
    public DateTimeOffset? PublishedAt { get; init; }

    /// <summary>Last change.</summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>The post's URL path.</summary>
    public string Url => "/posts/" + Slug;
}

namespace MiniBlog.Data.Posts;

/// <summary>Output-cache names shared by the public pages (host) and the admin plugin (which evicts).</summary>
public static class BlogCache
{
    /// <summary>The output cache policy of the public pages.</summary>
    public const string Policy = "blog-pages";

    /// <summary>The tag every public page carries; evicting it refreshes the site.</summary>
    public const string Tag = "posts";
}

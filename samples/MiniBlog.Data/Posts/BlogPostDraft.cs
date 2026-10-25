namespace MiniBlog.Data.Posts;

/// <summary>What the editor saves.</summary>
public sealed record BlogPostDraft(string Title, string Body, IReadOnlyList<string> Tags, bool Published, string Author);

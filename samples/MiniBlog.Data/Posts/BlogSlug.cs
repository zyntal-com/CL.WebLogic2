using System.Globalization;
using System.Text;

namespace MiniBlog.Data.Posts;

/// <summary>URL slugs from titles.</summary>
public static class BlogSlug
{
    /// <summary>"Hello, Wörld!" → "hello-world".</summary>
    public static string From(string title)
    {
        ArgumentNullException.ThrowIfNull(title);
        var builder = new StringBuilder();
        foreach (var c in title.Normalize(NormalizationForm.FormD).ToLowerInvariant())
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;
            if (char.IsAsciiLetterOrDigit(c))
                builder.Append(c);
            else if (builder.Length > 0 && builder[^1] != '-')
                builder.Append('-');
        }
        var slug = builder.ToString().Trim('-');
        return slug.Length == 0 ? "post" : slug[..Math.Min(slug.Length, 120)].Trim('-');
    }
}

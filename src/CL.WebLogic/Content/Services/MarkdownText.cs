using CL.WebLogic.Templates.Rendering;

namespace CL.WebLogic.Content.Services;

/// <summary>Small text helpers shared by the Markdown renderer.</summary>
internal static class MarkdownText
{
    /// <summary>Slug of heading text with Markdown punctuation removed.</summary>
    public static string Slug(string text) => TextTemplateFilters.Slugify(text.Replace("`", string.Empty, StringComparison.Ordinal));

    /// <summary>True for ASCII punctuation (backslash-escapable characters).</summary>
    public static bool IsAsciiPunctuation(char c) => c is >= '!' and <= '/' or >= ':' and <= '@' or >= '[' and <= '`' or >= '{' and <= '~';
}

namespace CL.WebLogic.Content.Models;

/// <summary>Kind of <see cref="HtmlToken"/>.</summary>
public enum HtmlTokenKind
{
    /// <summary>Character data (entities decoded).</summary>
    Text,

    /// <summary>A start tag.</summary>
    StartTag,

    /// <summary>An end tag.</summary>
    EndTag,

    /// <summary>A comment (or bogus comment such as <c>&lt;?xml…&gt;</c>).</summary>
    Comment,

    /// <summary>A <c>&lt;!DOCTYPE&gt;</c>.</summary>
    Doctype
}

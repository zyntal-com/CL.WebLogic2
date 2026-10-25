namespace CL.WebLogic.Content.Models;

/// <summary>One token produced by <see cref="Services.HtmlTokenizer"/>.</summary>
public sealed class HtmlToken
{
    /// <summary>Creates the token.</summary>
    public HtmlToken(HtmlTokenKind kind, string name, string text, IReadOnlyList<KeyValuePair<string, string>> attributes, bool selfClosing)
    {
        Kind = kind;
        Name = name;
        Text = text;
        Attributes = attributes;
        SelfClosing = selfClosing;
    }

    /// <summary>The kind.</summary>
    public HtmlTokenKind Kind { get; }

    /// <summary>Lower-case tag name for tags; empty otherwise.</summary>
    public string Name { get; }

    /// <summary>Decoded text for text tokens, raw content for comments.</summary>
    public string Text { get; }

    /// <summary>Attributes with lower-case names and decoded values (first occurrence wins).</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Attributes { get; }

    /// <summary>True for <c>&lt;br/&gt;</c>-style tags.</summary>
    public bool SelfClosing { get; }

    /// <summary>Creates a text token.</summary>
    public static HtmlToken ForText(string text) => new(HtmlTokenKind.Text, string.Empty, text, [], false);
}

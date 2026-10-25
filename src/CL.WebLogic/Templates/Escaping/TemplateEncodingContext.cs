namespace CL.WebLogic.Templates.Escaping;

/// <summary>
/// Where in the markup a template value is written. Inferred by the parser from the literal
/// text before the value, and used to pick the encoder at render time.
/// </summary>
public enum TemplateEncodingContext
{
    /// <summary>Element text (and comments): HTML-encoded.</summary>
    Html,

    /// <summary>A quoted attribute value: attribute-encoded.</summary>
    Attribute,

    /// <summary>A quoted URL attribute (href, src, action…): scheme-checked, then attribute-encoded.</summary>
    UrlAttribute,

    /// <summary>A quoted <c>style</c> attribute: restricted to safe CSS value characters.</summary>
    StyleAttribute,

    /// <summary>Inside <c>&lt;script&gt;</c>: only <c>|json</c> / <c>|js</c> values are allowed.</summary>
    Script,

    /// <summary>Inside <c>&lt;style&gt;</c>: only <c>{raw:…}</c> or <c>|css</c> values are allowed.</summary>
    Style,

    /// <summary>An inline event handler attribute (<c>onclick</c>…): never allowed (CSP-unsafe).</summary>
    EventHandler,

    /// <summary>Inside a tag but outside any attribute value: never allowed.</summary>
    TagBody,

    /// <summary>An unquoted attribute value: never allowed (quote the attribute).</summary>
    UnquotedAttribute,

    /// <summary>Not written to markup (e.g. widget attribute values passed as data): no encoding.</summary>
    Plain
}

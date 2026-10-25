using CL.WebLogic.Templates.Escaping;

namespace CL.WebLogic.Templates.Parsing;

/// <summary>
/// A small HTML tokenizer state machine fed with the template's literal text. It knows, at any
/// point, whether the next template value lands in element text, a quoted attribute (and which
/// one), a tag body, a script or style element, or a comment — enough to pick the encoder.
/// </summary>
internal sealed class HtmlContextTracker
{
    private static readonly HashSet<string> UrlAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "href", "src", "action", "formaction", "poster", "cite", "data", "background", "srcset",
        "xlink:href", "manifest", "longdesc", "icon", "ping", "codebase"
    };

    private State _state = State.Text;
    private string _tagName = string.Empty;
    private bool _closingTag;
    private string _attrName = string.Empty;
    private char _quote;
    private string _rawTextElement = string.Empty;
    private string _pending = string.Empty;
    private char _jsQuote;
    private bool _jsEscape;

    private enum State { Text, Tag, TagName, AttrName, AfterAttrName, BeforeAttrValue, AttrValueQuoted, AttrValueUnquoted, Comment, RawText }

    /// <summary>The encoding context for a value written at the current position.</summary>
    public TemplateEncodingContext Current => _state switch
    {
        // "<{value}" or "</{value}" would let the value name the tag: treat it like a tag body (refused).
        State.Text when _pending is "<" or "</" => TemplateEncodingContext.TagBody,
        State.Text or State.Comment => TemplateEncodingContext.Html,
        State.RawText => string.Equals(_rawTextElement, "script", StringComparison.OrdinalIgnoreCase)
            ? TemplateEncodingContext.Script
            : TemplateEncodingContext.Style,
        State.AttrValueQuoted => ForAttribute(_attrName),
        State.AttrValueUnquoted or State.BeforeAttrValue => TemplateEncodingContext.UnquotedAttribute,
        _ => TemplateEncodingContext.TagBody
    };

    /// <summary>True inside a quoted string literal of a script element (best effort: comments and regexes are not tracked).</summary>
    public bool InScriptString => _state == State.RawText && _jsQuote != '\0';

    /// <summary>True when the position is in element text (not in a tag, comment, script or style).</summary>
    public bool InText => _state == State.Text && _pending.Length == 0;

    /// <summary>Advances the state machine over literal template text.</summary>
    public void Feed(string text)
    {
        // A few lookaheads ("<!--", "</script") may straddle chunks; carry the tail over.
        var input = _pending + text;
        _pending = string.Empty;

        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            switch (_state)
            {
                case State.Text:
                    if (c == '<')
                    {
                        if (i + 1 >= input.Length) { _pending = "<"; return; }
                        if (StartsWith(input, i, "<!--")) { _state = State.Comment; i += 3; }
                        else if (input.Length - i < 4 && "<!--".StartsWith(input.Substring(i), StringComparison.Ordinal)) { _pending = input.Substring(i); return; }
                        else if (char.IsLetter(input[i + 1])) { _state = State.TagName; _tagName = string.Empty; _closingTag = false; }
                        else if (input[i + 1] == '/' && i + 2 >= input.Length) { _pending = "</"; return; }
                        else if (input[i + 1] == '/' && char.IsLetter(input[i + 2])) { _state = State.TagName; _tagName = string.Empty; _closingTag = true; i++; }
                    }
                    break;

                case State.Comment:
                    if (StartsWith(input, i, "-->")) { _state = State.Text; i += 2; }
                    break;

                case State.RawText:
                    var close = "</" + _rawTextElement;
                    if (StartsWith(input, i, close))
                    {
                        _state = State.TagName;
                        _tagName = string.Empty;
                        _closingTag = true;
                        i += 1;
                    }
                    else if (input.Length - i < close.Length && close.StartsWith(input.Substring(i), StringComparison.OrdinalIgnoreCase))
                    {
                        _pending = input.Substring(i);
                        return;
                    }
                    else if (_rawTextElement == "script")
                    {
                        TrackScriptString(c);
                    }
                    break;

                case State.TagName:
                    if (char.IsLetterOrDigit(c) || c is '-' or ':') _tagName += c;
                    else if (c == '>') EndTag();
                    else _state = State.Tag;
                    break;

                case State.Tag:
                    if (c == '>') EndTag();
                    else if (!char.IsWhiteSpace(c) && c != '/') { _state = State.AttrName; _attrName = c.ToString(); }
                    break;

                case State.AttrName:
                    if (c == '=') _state = State.BeforeAttrValue;
                    else if (c == '>') EndTag();
                    else if (char.IsWhiteSpace(c)) _state = State.AfterAttrName;
                    else _attrName += c;
                    break;

                case State.AfterAttrName:
                    if (c == '=') _state = State.BeforeAttrValue;
                    else if (c == '>') EndTag();
                    else if (!char.IsWhiteSpace(c) && c != '/') { _state = State.AttrName; _attrName = c.ToString(); }
                    break;

                case State.BeforeAttrValue:
                    if (c is '"' or '\'') { _quote = c; _state = State.AttrValueQuoted; }
                    else if (c == '>') EndTag();
                    else if (!char.IsWhiteSpace(c)) _state = State.AttrValueUnquoted;
                    break;

                case State.AttrValueQuoted:
                    if (c == _quote) _state = State.Tag;
                    break;

                case State.AttrValueUnquoted:
                    if (char.IsWhiteSpace(c)) _state = State.Tag;
                    else if (c == '>') EndTag();
                    break;
            }
        }
    }

    private void TrackScriptString(char c)
    {
        if (_jsQuote == '\0')
        {
            if (c is '"' or '\'' or '`') _jsQuote = c;
            return;
        }
        if (_jsEscape) _jsEscape = false;
        else if (c == '\\') _jsEscape = true;
        else if (c == _jsQuote) _jsQuote = '\0';
    }

    private void EndTag()
    {
        _jsQuote = '\0';
        _jsEscape = false;
        if (!_closingTag && (string.Equals(_tagName, "script", StringComparison.OrdinalIgnoreCase)
                             || string.Equals(_tagName, "style", StringComparison.OrdinalIgnoreCase)))
        {
            _state = State.RawText;
            _rawTextElement = _tagName.ToLowerInvariant();
            return;
        }

        _state = State.Text;
    }

    private static TemplateEncodingContext ForAttribute(string name)
    {
        // srcdoc is an HTML document the browser entity-decodes and runs: as dangerous as a handler.
        if ((name.StartsWith("on", StringComparison.OrdinalIgnoreCase) && name.Length > 2) || string.Equals(name, "srcdoc", StringComparison.OrdinalIgnoreCase))
            return TemplateEncodingContext.EventHandler;
        if (string.Equals(name, "style", StringComparison.OrdinalIgnoreCase))
            return TemplateEncodingContext.StyleAttribute;
        if (UrlAttributes.Contains(name))
            return TemplateEncodingContext.UrlAttribute;
        return TemplateEncodingContext.Attribute;
    }

    private static bool StartsWith(string input, int index, string value) =>
        index + value.Length <= input.Length &&
        string.Compare(input, index, value, 0, value.Length, StringComparison.OrdinalIgnoreCase) == 0;
}

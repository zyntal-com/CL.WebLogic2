using System.Text;
using CL.WebLogic.Templates.Ast;
using CL.WebLogic.Templates.Escaping;
using static CL.WebLogic.Templates.Parsing.TemplateMarkers;

namespace CL.WebLogic.Templates.Parsing;

/// <summary>
/// Recursive-descent parser turning template text into a <see cref="TemplateDocument"/>.
/// <para>
/// A <c>{</c> only starts a directive when a known keyword, scope or active loop alias follows
/// it immediately — anything else is literal text, so inline CSS / JS braces need no escaping.
/// Every value token records the markup context it appears in (see <see cref="HtmlContextTracker"/>);
/// values in unsafe positions are rejected with a <see cref="TemplateParseException"/>.
/// </para>
/// </summary>
public sealed partial class TemplateParser
{
    private static readonly string[] RawTextTags = ["script", "style"];

    private readonly string _src;
    private readonly string? _path;
    private readonly HtmlContextTracker _html = new();
    private readonly List<SectionDefinition> _sections = [];
    private readonly List<string> _aliases = [];
    private int _pos;

    private TemplateParser(string source, string? path)
    {
        _src = source ?? string.Empty;
        _path = path;
    }

    /// <summary>Parses a full template (honouring a leading <c>{layout:…}</c>).</summary>
    public static TemplateDocument Parse(string source, string? templatePath = null) =>
        new TemplateParser(source, templatePath).ParseDocument();

    private TemplateDocument ParseDocument()
    {
        var layout = TryConsumeLayout();
        var nodes = ParseNodes(TemplateStopKind.Document, out _);
        return new TemplateDocument(layout, nodes, _sections);
    }

    private string? TryConsumeLayout()
    {
        var i = 0;
        while (i < _src.Length && char.IsWhiteSpace(_src[i])) i++;
        if (i + 8 > _src.Length || string.Compare(_src, i, "{layout:", 0, 8, StringComparison.OrdinalIgnoreCase) != 0)
            return null;

        var close = _src.IndexOf('}', i);
        if (close < 0)
            return null;

        var layout = _src.Substring(i + 8, close - i - 8).Trim();
        var j = close + 1;
        while (j < _src.Length && char.IsWhiteSpace(_src[j])) j++;
        _pos = j;
        return layout;
    }

    private List<TemplateNode> ParseNodes(TemplateStopKind stop, out string? terminator)
    {
        terminator = null;
        var nodes = new List<TemplateNode>();
        var literal = new StringBuilder();

        void Flush()
        {
            if (literal.Length == 0) return;
            var text = literal.ToString();
            EmitLiteral(text, _pos - text.Length, nodes);
            literal.Clear();
        }

        while (_pos < _src.Length)
        {
            var c = _src[_pos];
            if (c == '{' && TryReadLegacyToken(out var legacy, out var afterLegacy))
            {
                Flush();
                nodes.Add(legacy);
                _pos = afterLegacy;
                continue;
            }

            var close = c == '{' ? FindDirectiveEnd(_pos) : -1;
            if (close < 0)
            {
                literal.Append(c);
                _pos++;
                continue;
            }

            var start = _pos;
            var body = _src.Substring(_pos + 1, close - _pos - 1);
            var end = close + 1;

            if (IsMarker(body))
            {
                if (!IsStop(stop, body))
                    throw Error($"Unexpected {{{body}}}.", start);
                Flush();
                terminator = body;
                _pos = end;
                return nodes;
            }

            var node = TryParseDirective(body, start, end, Flush);
            if (node is null)
            {
                // Not a directive: emit "{" and rescan from the next character.
                literal.Append(c);
                _pos++;
                continue;
            }

            if (node is not SectionPlaceholder)
                nodes.Add(node);
        }

        if (stop != TemplateStopKind.Document)
            throw Error($"Missing {Closer(stop)}.", _pos);

        Flush();
        return nodes;
    }

    /// <summary>Parses the directive at <paramref name="start"/>; returns null when it is not one.</summary>
    private TemplateNode? TryParseDirective(string body, int start, int end, Action flush)
    {
        if (Eq(body, "renderbody")) return Keyword(new KeywordNode(TemplateKeyword.RenderBody), "renderbody");
        if (Eq(body, "renderhead")) return Keyword(new KeywordNode(TemplateKeyword.RenderHead), "renderhead");
        if (Eq(body, "weblogic_scripts")) return Keyword(new KeywordNode(TemplateKeyword.WebLogicScripts), "weblogic_scripts");
        if (Eq(body, "nonce")) { flush(); _pos = end; return Checked(new KeywordNode(TemplateKeyword.Nonce), TemplateContextRules.ValidateTextDirective(_html.Current, "nonce")); }
        if (Eq(body, "csrf")) return Keyword(new CsrfNode(CsrfKind.Field), "csrf");
        if (Eq(body, "csrf_meta")) return Keyword(new CsrfNode(CsrfKind.Meta), "csrf_meta");
        if (Eq(body, "csrf_token")) { flush(); _pos = end; return Checked(new CsrfNode(CsrfKind.Token), TemplateContextRules.ValidateTextDirective(_html.Current, "csrf_token")); }
        if (Eq(body, "verbatim")) { flush(); _pos = end; return ReadVerbatim(start); }

        if (TryRest(body, "section:", out var section)) { flush(); _pos = end; ParseSection(section.Trim()); return SectionPlaceholder.Instance; }
        if (TryRest(body, "rendersection:", out var rendersection)) { flush(); _pos = end; return new RenderSectionNode(rendersection.Trim()); }
        if (TryRest(body, "foreach:", out var foreachRest)) { flush(); _pos = end; return ParseForeach(foreachRest, start); }
        if (TryRest(body, "ifnot:", out var ifnot)) { flush(); _pos = end; return ParseIf(ifnot, negate: true, start); }
        if (TryRest(body, "if:", out var ifRest)) { flush(); _pos = end; return ParseIf(ifRest, negate: false, start); }
        if (TryRest(body, "switch:", out var switchRest)) { flush(); _pos = end; return ParseSwitch(switchRest, start); }
        if (TryRest(body, "widgetarea:", out var area)) return Keyword(new WidgetAreaNode(area.Trim()), "widgetarea");
        if (TryRest(body, "form:", out var form)) return Keyword(ParseForm(form, start), "form");
        if (TryRest(body, "widget:", out var widget)) return Keyword(ParseWidget(widget, start), "widget");
        if (TryRest(body, "partial:", out var partial)) { flush(); _pos = end; return ParsePartial(partial, start); }
        if (TryRest(body, "asset:", out var asset)) { flush(); _pos = end; return Checked(new AssetNode(asset.Trim(), _html.Current), TemplateContextRules.ValidateTextDirective(_html.Current, "asset")); }
        if (TryRest(body, "t:", out var text)) { flush(); _pos = end; return Checked(new TextNode(text.Trim(), _html.Current), TemplateContextRules.ValidateTextDirective(_html.Current, "t")); }
        if (TryRest(body, "raw:", out var raw)) { flush(); _pos = end; return MakeToken(raw, raw: true, start) ?? throw Error($"Unknown scope in {{raw:{raw}}}.", start); }

        if (TryMakeBareItemToken(body, start) is { } bare)
        {
            flush();
            _pos = end;
            return bare;
        }

        if (!LooksLikeToken(body))
            return null;

        flush();
        var token = MakeToken(body, raw: false, start);
        if (token is null)
            return null; // unknown scope word (e.g. inline CSS "{color:red}") stays literal
        _pos = end;
        return token;

        TemplateNode Keyword(TemplateNode node, string name)
        {
            flush();
            _pos = end;
            return Checked(node, TemplateContextRules.ValidateMarkupDirective(_html.Current, name));
        }

        TemplateNode Checked(TemplateNode node, string? error) => error is null ? node : throw Error(error, start);
    }

    /// <summary>
    /// v1 <c>{{dotted.path}}</c> token: <c>page.x</c> and <c>item.x</c> read those scopes, anything
    /// else reads the model (then globals). Encoded for its context like any other token.
    /// </summary>
    private bool TryReadLegacyToken(out TokenNode token, out int after)
    {
        token = null!;
        after = _pos;
        if (_pos + 1 >= _src.Length || _src[_pos + 1] != '{')
            return false;

        var i = _pos + 2;
        var start = i;
        while (i < _src.Length && (char.IsAsciiLetterOrDigit(_src[i]) || _src[i] is '_' or '.' or '-')) i++;
        if (i == start || i + 1 >= _src.Length || _src[i] != '}' || _src[i + 1] != '}')
            return false;

        var dotted = _src.Substring(start, i - start);
        var selector = StartsWith(dotted, "page.") ? new TemplateSelector(TemplateScope.Page, dotted.Substring(5))
            : StartsWith(dotted, "item.") ? new TemplateSelector(TemplateScope.Item, dotted.Substring(5))
            : new TemplateSelector(TemplateScope.None, dotted);

        var context = _html.Current;
        var error = TemplateContextRules.ValidateToken(context, [], raw: false);
        if (error is not null)
            throw Error(error, _pos);

        token = new TokenNode(selector, [], raw: false, context);
        after = i + 2;
        return true;
    }

    /// <summary><c>{item}</c> or <c>{alias}</c> (optionally with filters): the current loop item itself.</summary>
    private TokenNode? TryMakeBareItemToken(string body, int start)
    {
        var (selectorText, filters) = SelectorSyntax.ParseExpression(body);
        TemplateSelector? selector = null;
        if (Eq(selectorText, "item"))
            selector = new TemplateSelector(TemplateScope.Item, string.Empty);
        else if (selectorText.Length > 0 && IsAlias(selectorText))
            selector = new TemplateSelector(TemplateScope.Alias, string.Empty, selectorText);

        if (selector is null)
            return null;

        var context = _html.Current;
        var error = TemplateContextRules.ValidateToken(context, filters, raw: false, _html.InScriptString, _html.InScriptTemplateLiteral);
        return error is null ? new TokenNode(selector, filters, raw: false, context) : throw Error(error, start);
    }

    private TokenNode? MakeToken(string expression, bool raw, int start)
    {
        var (selectorText, filters) = SelectorSyntax.ParseExpression(expression.Trim());
        var selector = SelectorSyntax.TryParseSelector(selectorText, IsAlias);
        if (selector is null)
            return null;

        var context = _html.Current;
        var error = TemplateContextRules.ValidateToken(context, filters, raw, _html.InScriptString, _html.InScriptTemplateLiteral);
        if (error is not null)
            throw Error(error, start);

        return new TokenNode(selector, filters, raw, context);
    }

    /// <summary>
    /// Adds literal text, inserting a nonce attribute into static <c>&lt;script&gt;</c> and
    /// <c>&lt;style&gt;</c> tags (in element text) that do not already carry one.
    /// </summary>
    private void EmitLiteral(string text, int absoluteStart, List<TemplateNode> nodes)
    {
        var fed = 0;
        var emitted = 0;
        for (var i = text.IndexOf('<'); i >= 0; i = text.IndexOf('<', i + 1))
        {
            var nameEnd = MatchRawTextTag(text, i);
            if (nameEnd < 0)
                continue;

            _html.Feed(text.Substring(fed, i - fed));
            fed = i;
            if (!_html.InText || SourceTagHasNonce(absoluteStart + nameEnd))
                continue;

            nodes.Add(new LiteralNode(text.Substring(emitted, nameEnd - emitted)));
            nodes.Add(new KeywordNode(TemplateKeyword.NonceAttribute));
            emitted = nameEnd;
        }

        _html.Feed(text.Substring(fed));
        if (emitted < text.Length)
            nodes.Add(new LiteralNode(text.Substring(emitted)));
    }

    /// <summary>Index just after <c>&lt;script</c> / <c>&lt;style</c> at <paramref name="index"/>, or -1.</summary>
    private static int MatchRawTextTag(string text, int index)
    {
        foreach (var name in RawTextTags)
        {
            var end = index + 1 + name.Length;
            if (end < text.Length
                && string.Compare(text, index + 1, name, 0, name.Length, StringComparison.OrdinalIgnoreCase) == 0
                && (char.IsWhiteSpace(text[end]) || text[end] is '>' or '/'))
                return end;
        }
        return -1;
    }

    /// <summary>True when the tag starting before <paramref name="position"/> already has a nonce (or never closes).</summary>
    private bool SourceTagHasNonce(int position)
    {
        var close = _src.IndexOf('>', position);
        return close < 0 || _src.IndexOf("nonce", position, close - position, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private VerbatimNode ReadVerbatim(int start)
    {
        const string closeTag = "{/verbatim}";
        var index = _src.IndexOf(closeTag, _pos, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            throw Error("Missing {/verbatim}.", start);

        var text = _src.Substring(_pos, index - _pos);
        _html.Feed(text);
        _pos = index + closeTag.Length;
        return new VerbatimNode(text);
    }

    /// <summary>Index of the <c>}</c> matching the <c>{</c> at <paramref name="open"/> (nested braces allowed), or -1.</summary>
    private int FindDirectiveEnd(int open)
    {
        var depth = 0;
        for (var i = open + 1; i < _src.Length; i++)
        {
            switch (_src[i])
            {
                case '{':
                    depth++;
                    break;
                case '}' when depth == 0:
                    return i;
                case '}':
                    depth--;
                    break;
                case '\n' when depth == 0:
                    return -1; // directives never span lines; this keeps multi-line JS/CSS blocks literal
            }
        }
        return -1;
    }

    private bool IsAlias(string word) => _aliases.Contains(word, StringComparer.OrdinalIgnoreCase);

    private static bool LooksLikeToken(string body)
    {
        var colon = body.IndexOf(':');
        if (colon <= 0 || colon == body.Length - 1)
            return false;
        for (var i = 0; i < colon; i++)
        {
            if (!char.IsAsciiLetter(body[i]) && body[i] != '_')
                return false;
        }
        return true;
    }

    private static bool TryRest(string body, string prefix, out string rest)
    {
        if (StartsWith(body, prefix))
        {
            rest = body.Substring(prefix.Length);
            return true;
        }

        rest = string.Empty;
        return false;
    }

    private TemplateParseException Error(string message, int position)
    {
        var line = 1;
        var column = 1;
        for (var i = 0; i < position && i < _src.Length; i++)
        {
            if (_src[i] == '\n') { line++; column = 1; }
            else column++;
        }
        return new TemplateParseException(message, line, column, _path);
    }

    /// <summary>Marker returned for <c>{section}</c> blocks, which are collected rather than emitted.</summary>
    private sealed class SectionPlaceholder : TemplateNode
    {
        public static readonly SectionPlaceholder Instance = new();
    }
}

using CL.WebLogic.Templates.Ast;
using CL.WebLogic.Templates.Escaping;
using static CL.WebLogic.Templates.Parsing.TemplateMarkers;

namespace CL.WebLogic.Templates.Parsing;

// Block directives: foreach, section, if/elseif/else, switch, partial and widget.
public sealed partial class TemplateParser
{
    // Words that already mean something before a ':' inside {…}. A foreach alias with one of these names is shadowed
    // ({t:label} prints the text key "label") or opens a block ({section:x}), so it is refused up front.
    private static readonly HashSet<string> ReservedAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        "section", "rendersection", "foreach", "if", "ifnot", "elseif", "switch", "case", "widgetarea", "form", "widget",
        "partial", "asset", "t", "raw", "model", "page", "query", "route", "item", "param", "loop", "user"
    };

    private ForeachNode ParseForeach(string rest, int start)
    {
        var source = rest.Trim();
        string? alias = null;
        var asIndex = source.IndexOf(" as ", StringComparison.OrdinalIgnoreCase);
        if (asIndex >= 0)
        {
            alias = source.Substring(asIndex + 4).Trim();
            source = source.Substring(0, asIndex).Trim();
            if (ReservedAliases.Contains(alias))
                throw Error($"'{alias}' can't be a foreach alias in {{foreach:{rest}}}: {{{alias}:…}} already means something in templates. Pick another name.", start);
        }

        var selector = SelectorSyntax.TryParseSelector(source, IsAlias) ?? throw Error($"Unknown scope in {{foreach:{rest}}}.", start);
        if (alias is not null) _aliases.Add(alias);
        try
        {
            var body = ParseNodes(TemplateStopKind.Foreach, out var term);
            IReadOnlyList<TemplateNode>? empty = null;
            if (term is not null && Eq(term, "empty"))
                empty = ParseNodes(TemplateStopKind.Foreach, out _);
            return new ForeachNode(selector, string.IsNullOrEmpty(alias) ? null : alias, body, empty);
        }
        finally
        {
            if (alias is not null) _aliases.RemoveAt(_aliases.Count - 1);
        }
    }

    private void ParseSection(string name)
    {
        var body = ParseNodes(TemplateStopKind.Section, out _);
        _sections.Add(new SectionDefinition(name, body));
    }

    private IfNode ParseIf(string rest, bool negate, int start)
    {
        var condition = SelectorSyntax.TryParseCondition(rest, IsAlias) ?? throw Error($"Unknown scope in condition '{rest}'.", start);
        if (negate) condition = new NotCondition(condition);

        var stop = negate ? TemplateStopKind.IfNotBody : TemplateStopKind.IfBody;
        var branches = new List<ConditionalBranch> { new(condition, ParseNodes(stop, out var term)) };
        IReadOnlyList<TemplateNode>? elseBody = null;

        while (term is not null && StartsWith(term, "elseif:"))
        {
            var next = SelectorSyntax.TryParseCondition(term.Substring(7), IsAlias) ?? throw Error($"Unknown scope in condition '{term}'.", start);
            branches.Add(new ConditionalBranch(next, ParseNodes(stop, out term)));
        }

        if (term is not null && Eq(term, "else"))
            elseBody = ParseNodes(stop, out _);

        return new IfNode(branches, elseBody);
    }

    private SwitchNode ParseSwitch(string rest, int start)
    {
        var subject = SelectorSyntax.TryParseSelector(rest.Trim(), IsAlias) ?? throw Error($"Unknown scope in {{switch:{rest}}}.", start);
        var cases = new List<SwitchCase>();
        IReadOnlyList<TemplateNode>? defaultBody = null;

        ParseNodes(TemplateStopKind.SwitchBody, out var term);
        while (term is not null && !Eq(term, "/switch"))
        {
            var isCase = StartsWith(term, "case:");
            if (!isCase && !Eq(term, "default"))
                throw Error($"Unexpected {{{term}}} in switch.", start);

            var value = isCase ? term.Substring(5).Trim().Trim('"', '\'') : null;
            var body = ParseNodes(isCase ? TemplateStopKind.CaseBody : TemplateStopKind.DefaultBody, out var t);
            if (isCase) cases.Add(new SwitchCase(value!, body));
            else defaultBody = body;

            if (t is not null && (Eq(t, "/case") || Eq(t, "/default")))
                ParseNodes(TemplateStopKind.SwitchBody, out term);
            else
                term = t;
        }

        return new SwitchNode(subject, cases, defaultBody);
    }

    private PartialNode ParsePartial(string rest, int start)
    {
        var (path, attributes) = TemplateAttributeSyntax.SplitFirstToken(rest);
        var arguments = new List<PartialArgument>();
        foreach (var (name, value, quoted) in TemplateAttributeSyntax.Parse(attributes))
        {
            arguments.Add(quoted
                ? PartialArgument.FromLiteral(name, value)
                : PartialArgument.FromSelector(name, SelectorSyntax.TryParseSelector(value, IsAlias)
                    ?? throw Error($"Unknown scope in partial argument '{name}'.", start)));
        }
        return new PartialNode(path, arguments);
    }

    private WidgetNode ParseWidget(string rest, int start)
    {
        var (name, attributes) = TemplateAttributeSyntax.SplitFirstToken(rest);
        var parsed = new List<WidgetArgument>();
        foreach (var (attrName, value, _) in TemplateAttributeSyntax.Parse(attributes))
            parsed.Add(new WidgetArgument(attrName, ParseInlineValue(value, start)));
        return new WidgetNode(name, parsed);
    }

    private FormNode ParseForm(string rest, int start)
    {
        var widget = ParseWidget(rest, start);
        return new FormNode(widget.Name, widget.Attributes);
    }

    /// <summary>Widget attribute values are data, not markup: literals plus plain (unencoded) tokens.</summary>
    private List<TemplateNode> ParseInlineValue(string value, int start)
    {
        var nodes = new List<TemplateNode>();
        var i = 0;
        while (i < value.Length)
        {
            var open = value.IndexOf('{', i);
            var close = open < 0 ? -1 : value.IndexOf('}', open + 1);
            if (open < 0 || close < 0)
            {
                nodes.Add(new LiteralNode(value.Substring(i)));
                break;
            }

            if (open > i) nodes.Add(new LiteralNode(value.Substring(i, open - i)));
            var (selectorText, filters) = SelectorSyntax.ParseExpression(value.Substring(open + 1, close - open - 1));
            var selector = SelectorSyntax.TryParseSelector(selectorText, IsAlias) ?? throw Error($"Unknown scope in widget attribute '{value}'.", start);
            nodes.Add(new TokenNode(selector, filters, raw: false, TemplateEncodingContext.Plain));
            i = close + 1;
        }
        return nodes;
    }
}

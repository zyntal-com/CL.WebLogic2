using CL.WebLogic.Templates.Ast;
using CL.WebLogic.Templates.Escaping;
using CL.WebLogic.Templates.Parsing;

namespace CL.WebLogic.Tests.Templates;

public sealed class TemplateParserTests
{
    private static T Single<T>(string source) where T : TemplateNode =>
        TemplateParser.Parse(source).Nodes.OfType<T>().Single();

    private static TemplateEncodingContext ContextOf(string source) => Single<TokenNode>(source).Context;

    [Theory]
    [InlineData("<p>{model:name}</p>", TemplateEncodingContext.Html)]
    [InlineData("<!-- {model:name} -->", TemplateEncodingContext.Html)]
    [InlineData("<div title=\"{model:name}\"></div>", TemplateEncodingContext.Attribute)]
    [InlineData("<div title='{model:name}'></div>", TemplateEncodingContext.Attribute)]
    [InlineData("<a href=\"{model:url}\">x</a>", TemplateEncodingContext.UrlAttribute)]
    [InlineData("<img class=\"x\" src=\"/img/{model:file}\">", TemplateEncodingContext.ImageUrlAttribute)]
    [InlineData("<script src=\"/js/{model:file}\">", TemplateEncodingContext.UrlAttribute)]
    [InlineData("<form action=\"{model:url}\"></form>", TemplateEncodingContext.UrlAttribute)]
    [InlineData("<div style=\"color: {model:c}\"></div>", TemplateEncodingContext.StyleAttribute)]
    [InlineData("<input data-id=\"{model:id}\" value=\"{raw:model:x}\">", TemplateEncodingContext.Attribute)]
    [InlineData("<script>var x = {model:data|json};</script>", TemplateEncodingContext.Script)]
    [InlineData("<p>a</p><script src=\"/x.js\"></script><p>{model:name}</p>", TemplateEncodingContext.Html)]
    public void InfersEncodingContext(string source, TemplateEncodingContext expected) =>
        Assert.Equal(expected, TemplateParser.Parse(source).Nodes.OfType<TokenNode>().First().Context);

    [Theory]
    [InlineData("<div {model:attrs}></div>")]
    [InlineData("<a href={model:url}>x</a>")]
    [InlineData("<button onclick=\"go('{model:id}')\">x</button>")]
    [InlineData("<script>var name = '{model:name}';</script>")]
    [InlineData("<style>.x { color: {model:c}; }</style>")]
    [InlineData("<p><{model:tag}></p>")] // security review: the value would name the tag
    [InlineData("<p></{model:tag}></p>")]
    [InlineData("<iframe srcdoc=\"{model:html}\"></iframe>")] // security review: srcdoc is decoded and run as HTML
    [InlineData("<script>var n = {model:n|js};</script>")] // security review: |js outside quotes is code
    public void RejectsUnsafePositions(string source) =>
        Assert.Throws<TemplateParseException>(() => TemplateParser.Parse(source));

    [Theory]
    [InlineData("<script>var n = '{model:n|js}';</script>")]
    [InlineData("<script>var n = \"a {model:n|js} b\"; var m = {model:m|json};</script>")]
    [InlineData("<script>var s = 'it\\'s'; var n = '{model:n|js}';</script>")]
    public void AllowsJsInsideQuotedStrings(string source) => TemplateParser.Parse(source);

        [Fact]
    public void Raw_IsAllowedAnywhere_AsExplicitTrust()
    {
        var token = Single<TokenNode>("<div {raw:model:attrs}></div>");
        Assert.True(token.Raw);
    }

    [Fact]
    public void UnknownScopeWords_StayLiteral()
    {
        var doc = TemplateParser.Parse("<style>.a{color:red}</style><p>{name}</p>");

        Assert.Empty(doc.Nodes.OfType<TokenNode>());
        Assert.Equal("<style>.a{color:red}</style><p>{name}</p>", string.Concat(doc.Nodes.OfType<LiteralNode>().Select(n => n.Text)));
    }

    [Fact]
    public void Foreach_AliasIsScopedToTheLoop()
    {
        var doc = TemplateParser.Parse("{foreach:model:rows as row}<td>{row:name}</td>{empty}none{/foreach}<p>{row:name}</p>");

        var loop = doc.Nodes.OfType<ForeachNode>().Single();
        var token = loop.Body.OfType<TokenNode>().Single();
        Assert.Equal(TemplateScope.Alias, token.Selector.Scope);
        Assert.Equal("row", token.Selector.Alias);
        Assert.NotNull(loop.Empty);
        Assert.Empty(doc.Nodes.OfType<TokenNode>()); // {row:name} outside the loop is literal
    }

    [Fact]
    public void If_ElseIf_Else()
    {
        var node = Single<IfNode>("{if:model:count gt 10}many{elseif:group:admin,editor}staff{else}few{/if}");

        Assert.Equal(2, node.Branches.Count);
        var compare = Assert.IsType<CompareCondition>(node.Branches[0].Condition);
        Assert.Equal(CompareOp.Gt, compare.Op);
        Assert.Equal("10", compare.Right.Literal);
        Assert.Equal(["admin", "editor"], Assert.IsType<GroupCondition>(node.Branches[1].Condition).Groups);
        Assert.NotNull(node.Else);
    }

    [Fact]
    public void Switch_WithCasesAndDefault()
    {
        var node = Single<SwitchNode>("{switch:model:status}{case:draft}D{/case}{case:\"published\"}P{/case}{default}?{/default}{/switch}");

        Assert.Equal(["draft", "published"], node.Cases.Select(c => c.Value));
        Assert.NotNull(node.Default);
    }

    [Fact]
    public void Layout_AndSections()
    {
        var doc = TemplateParser.Parse("{layout:layouts/main}\n{section:scripts}<i>s</i>{/section}<h1>Body</h1>");

        Assert.Equal("layouts/main", doc.LayoutPath);
        Assert.Equal("scripts", doc.Sections.Single().Name);
        Assert.Equal("<h1>Body</h1>", Assert.IsType<LiteralNode>(doc.Nodes.Single()).Text);
    }

    [Fact]
    public void Partial_LiteralAndSelectorArguments()
    {
        var node = Single<PartialNode>("{partial:partials/card title=\"Hello\" rows=model:items}");

        Assert.Equal("partials/card", node.Path);
        Assert.True(node.Arguments[0].IsLiteral);
        Assert.Equal(TemplateScope.Model, node.Arguments[1].Selector!.Scope);
    }

    [Fact]
    public void WidgetArguments_ArePlainData()
    {
        var node = Single<WidgetNode>("{widget:latest-posts take=\"5\" tag=\"{model:tag}\"}");

        var token = node.Attributes[1].Value.OfType<TokenNode>().Single();
        Assert.Equal(TemplateEncodingContext.Plain, token.Context);
    }

    [Fact]
    public void Verbatim_IsCopiedWithoutParsing() =>
        Assert.Equal("{model:x} {if:auth}", Single<VerbatimNode>("{verbatim}{model:x} {if:auth}{/verbatim}").Text);

    [Fact]
    public void Filters_AreParsed_WithQuotedPipes()
    {
        var token = Single<TokenNode>("<p>{model:title|replace:\"a|b\"|truncate:20|upper}</p>");

        Assert.Equal(["replace", "truncate", "upper"], token.Filters.Select(f => f.Name));
        Assert.Equal("a|b", token.Filters[0].Arg);
    }

    [Fact]
    public void MissingCloser_ReportsLine()
    {
        var ex = Assert.Throws<TemplateParseException>(() => TemplateParser.Parse("<ul>\n{foreach:model:x}\n<li></li>\n", "templates/list.html"));

        Assert.Contains("{/foreach}", ex.Message);
        Assert.Contains("templates/list.html", ex.Message);
    }

    [Theory]
    [InlineData("t")]
    [InlineData("section")]
    [InlineData("Item")]
    [InlineData("loop")]
    public void ForeachAlias_ThatIsATemplateKeyword_IsAnError(string alias)
    {
        var ex = Assert.Throws<TemplateParseException>(() => TemplateParser.Parse($"<ul>{{foreach:model:tabs as {alias}}}<li>{{{alias}:label}}</li>{{/foreach}}</ul>"));

        Assert.Contains($"'{alias}' can't be a foreach alias", ex.Message);
    }

    [Fact]
    public void ForeachAlias_GroupIsAllowed() =>
        Assert.NotNull(TemplateParser.Parse("<ul>{foreach:model:groups as group}<li>{group:name}</li>{/foreach}</ul>"));

    [Fact]
    public void StrayMarker_IsAnError() =>
        Assert.Throws<TemplateParseException>(() => TemplateParser.Parse("<p>{/if}</p>"));

    [Theory]
    [InlineData("<div {csrf}></div>")]
    [InlineData("<a href=\"{renderhead}\"></a>")]
    [InlineData("<script>{t:greeting}</script>")]
    public void MarkupDirectives_AreRestrictedToTheirContexts(string source) =>
        Assert.Throws<TemplateParseException>(() => TemplateParser.Parse(source));

    [Fact]
    public void AssetAndText_InAttributes()
    {
        var doc = TemplateParser.Parse("<link href=\"{asset:css/site.css}\"><span title=\"{t:hello}\"></span><script nonce=\"{nonce}\"></script>");

        Assert.Equal(TemplateEncodingContext.UrlAttribute, doc.Nodes.OfType<AssetNode>().Single().Context);
        Assert.Equal(TemplateEncodingContext.Attribute, doc.Nodes.OfType<TextNode>().Single().Context);
        Assert.Single(doc.Nodes.OfType<KeywordNode>());
    }
}

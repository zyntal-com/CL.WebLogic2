using System.Security.Claims;
using CL.WebLogic.Templates.Escaping;
using CL.WebLogic.Templates.Rendering;
using CL.WebLogic.Testing.Services;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Tests.Templates;

/// <summary>Every case runs on both engines; their output must be identical.</summary>
public sealed class TemplateRenderingTests
{
    private static readonly int[] Rows = [1, 2];

    public static TheoryData<string> Engines => ["closure", "interpreter"];

    private static TemplateTestRenderer Renderer(string engine) =>
        new(engine == "closure" ? new ClosureTemplateCompiler() : new TemplateInterpreter());

    private static Dictionary<string, object?> Model(params (string Key, object? Value)[] values) =>
        values.ToDictionary(v => v.Key, v => v.Value, StringComparer.OrdinalIgnoreCase);

    [Theory, MemberData(nameof(Engines))]
    public async Task EncodesByContext(string engine)
    {
        var html = await Renderer(engine).RenderSourceAsync(
            "<p title=\"{model:v}\">{model:v}</p><a href=\"{model:url}\">x</a><script>var d = {model:v|json};</script>",
            Model(("v", "<b>\"x\" & 'y'</b>"), ("url", "javascript:alert(1)")));

        Assert.Equal(
            "<p title=\"&lt;b&gt;&quot;x&quot; &amp; &#x27;y&#x27;&lt;/b&gt;\">&lt;b&gt;&quot;x&quot; &amp; &#x27;y&#x27;&lt;/b&gt;</p>" +
            "<a href=\"about:blank#blocked\">x</a>" +
            "<script>var d = \"\\u003Cb\\u003E\\u0022x\\u0022 \\u0026 \\u0027y\\u0027\\u003C/b\\u003E\";</script>",
            html);
    }

    [Theory, MemberData(nameof(Engines))]
    public async Task WebHtml_IsTrustedOnlyInElementText(string engine)
    {
        var html = await Renderer(engine).RenderSourceAsync(
            "<div>{model:h}</div><i title=\"{model:h}\"></i>", Model(("h", new WebHtml("<b>ok</b>"))));

        Assert.Equal("<div><b>ok</b></div><i title=\"&lt;b&gt;ok&lt;/b&gt;\"></i>", html);
    }

    [Theory, MemberData(nameof(Engines))]
    public async Task Nl2br_EncodesBeforeBreaking(string engine)
    {
        var html = await Renderer(engine).RenderSourceAsync("<p>{model:t|nl2br}</p>", Model(("t", "a<script>\nb")));
        Assert.Equal("<p>a&lt;script&gt;<br>b</p>", html);
    }

    [Theory, MemberData(nameof(Engines))]
    public async Task Foreach_WithAliasLoopMetaAndEmpty(string engine)
    {
        var renderer = Renderer(engine);
        const string source = "{foreach:model:rows as r}{r:name}{if:loop:last}.{else},{/if}{empty}none{/foreach}";

        Assert.Equal("a,b.", await renderer.RenderSourceAsync(source, Model(("rows", new[] { new { Name = "a" }, new { Name = "b" } }))));
        Assert.Equal("none", await renderer.RenderSourceAsync(source, Model(("rows", Array.Empty<object>()))));
    }

    [Theory, MemberData(nameof(Engines))]
    public async Task Conditions_CompareSwitchAndAuth(string engine)
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Role, "editor"), new Claim("permission", "posts.*")], "test"))
        };
        var html = await Renderer(engine).RenderSourceAsync(
            "{if:model:n gte 10}big{elseif:model:n gt 5}mid{else}small{/if}|" +
            "{switch:model:s}{case:draft}D{/case}{default}?{/default}{/switch}|" +
            "{if:auth}in{/if}{if:group:admin,editor}E{/if}{if:permission:posts.edit}P{/if}{ifnot:permission:users.edit}!{/ifnot}",
            Model(("n", 7), ("s", "DRAFT")), http);

        Assert.Equal("mid|D|inEP!", html);
    }

    [Theory, MemberData(nameof(Engines))]
    public async Task Layout_SectionsAndNestedLayouts(string engine)
    {
        var renderer = Renderer(engine);
        renderer.Templates
            .Add("layouts/base", "<html>{rendersection:head}<body>{renderbody}</body></html>")
            .Add("layouts/main", "{layout:layouts/base}<main>{renderbody}</main>{section:head}<i>main</i>{/section}")
            .Add("templates/page", "{layout:layouts/main}{section:head}<b>page</b>{/section}<h1>{model:title}</h1>");

        var html = await renderer.RenderAsync("templates/page", Model(("title", "Hi")));

        Assert.Equal("<html><b>page</b><body><main><h1>Hi</h1></main></body></html>", html);
    }

    [Theory, MemberData(nameof(Engines))]
    public async Task Img_src_allows_raster_data_images_only(string engine)
    {
        const string png = "data:image/png;base64,iVBORw0KGgo=";
        var html = await Renderer(engine).RenderSourceAsync(
            "<img src=\"{model:png}\"><img src=\"{model:svg}\"><a href=\"{model:png}\">x</a><img srcset=\"{model:png}\"><img alt=\"q\" src=\"{model:js}\">",
            Model(("png", png), ("svg", "data:image/svg+xml;base64,PHN2Zz4="), ("js", "javascript:alert(1)")));

        Assert.Equal(
            $"<img src=\"{png}\"><img src=\"about:blank#blocked\"><a href=\"about:blank#blocked\">x</a>" +
            "<img srcset=\"about:blank#blocked\"><img alt=\"q\" src=\"about:blank#blocked\">",
            html);
    }

    [Theory, MemberData(nameof(Engines))]
    public async Task Scripts_learn_the_document_layout(string engine)
    {
        var renderer = Renderer(engine);
        var scripts = new LayoutCapturingScripts();
        renderer.Scripts = scripts;
        renderer.Templates
            .Add("layouts/base", "<html><body>{renderbody}{weblogic_scripts}</body></html>")
            .Add("layouts/main", "{layout:layouts/base}<main>{renderbody}</main>")
            .Add("templates/page", "{layout:layouts/main}<h1>x</h1>")
            .Add("templates/bare", "<p>{weblogic_scripts}</p>");

        await renderer.RenderAsync("templates/page");
        Assert.Equal("layouts/base.html", scripts.Layout);
        await renderer.RenderAsync("templates/bare");
        Assert.Equal("templates/bare.html", scripts.Layout);
        await renderer.RenderAsync("templates/bare", ignoreLayout: true);
        Assert.Null(scripts.Layout);
    }

    private sealed class LayoutCapturingScripts : ITemplateScriptProvider
    {
        public string? Layout { get; private set; }

        public string GetScriptTags(HttpContext? httpContext, string nonce) => string.Empty;

        public string GetScriptTags(HttpContext? httpContext, string nonce, string? layout)
        {
            Layout = layout;
            return string.Empty;
        }
    }

    [Theory, MemberData(nameof(Engines))]
    public async Task Partials_WithParameters_AndRecursionGuard(string engine)
    {
        var renderer = Renderer(engine);
        renderer.Templates
            .Add("partials/card", "<div>{param:title}:{foreach:param:rows}{item}{/foreach}</div>")
            .Add("partials/loop", "{partial:partials/loop}");

        Assert.Equal("<div>T:12</div>", await renderer.RenderSourceAsync("{partial:partials/card title=\"T\" rows=model:rows}", Model(("rows", Rows))));
        await Assert.ThrowsAsync<TemplateRenderException>(() => renderer.RenderSourceAsync("{partial:partials/loop}"));
    }

    [Theory, MemberData(nameof(Engines))]
    public async Task AutoNonce_OnStaticScriptAndStyleTags(string engine)
    {
        var http = new DefaultHttpContext();
        var renderer = Renderer(engine);
        var html = await renderer.RenderSourceAsync("<script src=\"/a.js\"></script><style>p{}</style><script nonce=\"{nonce}\"></script>", null, http);
        var nonce = renderer.Nonce.GetNonce(http);

        Assert.Equal($"<script nonce=\"{nonce}\" src=\"/a.js\"></script><style nonce=\"{nonce}\">p{{}}</style><script nonce=\"{nonce}\"></script>", html);
    }

    [Theory, MemberData(nameof(Engines))]
    public async Task Filters_TextAndNumbers(string engine)
    {
        var html = await Renderer(engine).RenderSourceAsync(
            "{model:t|truncate:5}|{model:t|upper}|{model:t|slug}|{model:missing|default:none}|{model:n|abbreviate}|{model:b|filesize}|{model:t|replace:\"Hello->Bye\"}",
            Model(("t", "Hello World"), ("n", 15300), ("b", 2048)));

        Assert.Equal("Hello...|HELLO WORLD|hello-world|none|15.3k|2 KB|Bye World", html);
    }

    [Theory, MemberData(nameof(Engines))]
    public async Task Markdown_AndSanitize_Filters(string engine)
    {
        var html = await Renderer(engine).RenderSourceAsync(
            "<div>{model:md|markdown}</div><div>{model:h|sanitize}</div>",
            Model(("md", "**hi** <script>"), ("h", "<b onclick=\"x()\">ok</b><script>bad()</script>")));

        Assert.Equal("<div><p><strong>hi</strong> &lt;script&gt;</p></div><div><b>ok</b></div>", html);
    }

    [Theory, MemberData(nameof(Engines))]
    public async Task Widgets_ReceivePlainAttributes(string engine)
    {
        var widgets = new RecordingWidgets();
        var renderer = Renderer(engine);
        renderer.Widgets = widgets;

        await renderer.RenderSourceAsync("{widget:latest take=\"5\" tag=\"{model:tag}\"}", Model(("tag", "<x>")));

        Assert.Equal("latest", widgets.Name);
        Assert.Equal("5", widgets.Attributes!["take"]);
        Assert.Equal("<x>", widgets.Attributes["tag"]); // data, not markup — the widget encodes on output
    }

    [Theory, MemberData(nameof(Engines))]
    public async Task UnknownFilter_PassesValueThrough(string engine) =>
        Assert.Equal("&lt;1&gt;", await Renderer(engine).RenderSourceAsync("{model:x|nope}", Model(("x", "<1>"))));

    [Theory, MemberData(nameof(Engines))]
    public async Task LegacyDoubleBraceTokens_AreSupportedAndEncoded(string engine)
    {
        var html = await Renderer(engine).RenderSourceAsync(
            "<p title=\"{{user.name}}\">{{title}}</p>{foreach:model:rows}{{item.n}}{/foreach}",
            Model(("title", "<T>"), ("user", new { Name = "A\"B" }), ("rows", new[] { new { N = 1 }, new { N = 2 } })));

        Assert.Equal("<p title=\"A&quot;B\">&lt;T&gt;</p>12", html);
    }

    [Fact]
    public async Task CustomFilter_IsUsed()
    {
        var renderer = new TemplateTestRenderer();
        renderer.Filters.Add(new ShoutFilter());
        Assert.Equal("HI!", await renderer.RenderSourceAsync("{model:x|shout}", Model(("x", "hi"))));
    }

    [Fact]
    public void TemplatePath_RejectsTraversal()
    {
        Assert.Equal("partials/card.html", TemplatePath.Normalize("partials/card", "templates/home.html"));
        Assert.Equal("templates/shared/x.html", TemplatePath.Normalize("shared/x", "templates/home.html"));
        Assert.Throws<TemplateRenderException>(() => TemplatePath.Normalize("../../etc/passwd"));
        Assert.Throws<TemplateRenderException>(() => TemplatePath.Normalize("templates/.hidden"));
        Assert.Throws<TemplateRenderException>(() => TemplatePath.Normalize("c:/windows/x"));
    }

    private sealed class ShoutFilter : ITemplateFilter
    {
        public string Name => "shout";

        public object? Apply(object? value, string? argument, TemplateRenderContext context) =>
            TemplateValues.ToText(value).ToUpperInvariant() + "!";
    }

    private sealed class RecordingWidgets : ITemplateWidgetRenderer
    {
        public string? Name { get; private set; }

        public IReadOnlyDictionary<string, string>? Attributes { get; private set; }

        public Task RenderWidgetAsync(TemplateRenderContext context, string name, IReadOnlyDictionary<string, string> attributes)
        {
            Name = name;
            Attributes = attributes;
            return Task.CompletedTask;
        }

        public Task RenderAreaAsync(TemplateRenderContext context, string area) => Task.CompletedTask;
    }
}

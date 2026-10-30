using System.Text.Json;
using CL.WebLogic.Routing.Models;
using CL.WebLogic.Runtime.Models;
using CL.WebLogic.Runtime.Services;
using CL.WebLogic.Templates.Rendering;
using CL.WebLogic.Testing.Services;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Tests.Runtime;

public sealed class WebPageLinksTests
{
    [Fact]
    public void PageUrl_KeepsOtherQueryValues_AndReplacesPage()
    {
        var url = WebPageLinks.PageUrl("/posts", "?q=hello&sort=new&page=2&tag=a&tag=b", 3);

        Assert.Equal("/posts?q=hello&sort=new&tag=a&tag=b&page=3", url);
    }

    [Fact]
    public void PageUrl_PageOne_DropsTheParameter()
    {
        Assert.Equal("/posts?q=x", WebPageLinks.PageUrl("/posts", "?PAGE=4&q=x", 1));
        Assert.Equal("/", WebPageLinks.PageUrl("/", null, 1));
    }

    [Fact]
    public void PageUrl_EncodesPathKeysAndValues()
    {
        var url = WebPageLinks.PageUrl("/tags/c# & more", "?q=%3Cscript%3E%22&x%20y=1", 2);

        Assert.Equal("/tags/c%23%20&%20more?q=%3Cscript%3E%22&x%20y=1&page=2", url);
        Assert.DoesNotContain("<", url, StringComparison.Ordinal);
        Assert.DoesNotContain("\"", url, StringComparison.Ordinal);
    }

    [Fact]
    public void PageUrl_CustomParameter()
    {
        Assert.Equal("/list?page=9&p=2", WebPageLinks.PageUrl("/list", "?page=9", 2, "p"));
    }

    [Theory]
    [InlineData("//evil.example")]
    [InlineData("https://evil.example/")]
    [InlineData("/\\evil.example")]
    [InlineData("relative")]
    public void PageUrl_RefusesNonLocalPaths(string path) =>
        Assert.Throws<ArgumentException>(() => WebPageLinks.PageUrl(path, null, 2));

    [Fact]
    public void Build_ProducesWindowAndEdges()
    {
        var links = WebPageLinks.Build("/blog", "?q=x", WebPagination.Create(5, 10, 95), window: 2);

        Assert.Equal("/blog?q=x&page=4", links.PreviousUrl);
        Assert.Equal("/blog?q=x&page=6", links.NextUrl);
        Assert.Equal("/blog?q=x", links.FirstUrl);
        Assert.Equal("/blog?q=x&page=10", links.LastUrl);
        Assert.Equal([3, 4, 5, 6, 7], links.Pages.Select(p => p.Number));
        Assert.True(links.Pages.Single(p => p.IsCurrent).Number == 5);
    }

    [Fact]
    public void Build_OnFirstAndLastPage_HasNoDeadLinks()
    {
        var first = WebPageLinks.Build("/", null, WebPagination.Create(1, 10, 15));
        var only = WebPageLinks.Build("/", null, WebPagination.Create(1, 10, 3));

        Assert.Null(first.PreviousUrl);
        Assert.Null(first.FirstUrl);
        Assert.Equal("/?page=2", first.NextUrl);
        Assert.Null(only.NextUrl);
        Assert.Null(only.LastUrl);
        Assert.Single(only.Pages);
    }

    [Fact]
    public void Build_FromRequest_KeepsPathBase_AndNeverEmitsProtocolRelativeLinks()
    {
        var http = new DefaultHttpContext();
        http.Request.PathBase = "/app";
        http.Request.Path = "/list";
        http.Request.QueryString = new QueryString("?q=a+b&page=2");
        Assert.Equal("/app/list?q=a%20b&page=3", WebPageLinks.PageUrl(Request(http), 3));

        var evil = new DefaultHttpContext();
        evil.Request.Path = "//evil.example";
        var url = WebPageLinks.PageUrl(Request(evil), 2);
        Assert.Equal("/evil.example?page=2", url);
        Assert.True(WebUrl.IsLocal(url));
    }

    [Theory]
    [InlineData("closure")]
    [InlineData("interpreter")]
    public async Task DocumentedPagerPartial_RendersInBothEngines(string engine)
    {
        var renderer = new TemplateTestRenderer(engine == "closure" ? new ClosureTemplateCompiler() : new TemplateInterpreter());
        var pager = WebPageLinks.Build("/posts", "?q=<b>", WebPagination.Create(2, 10, 30));

        var html = await renderer.RenderSourceAsync("""
            {if:model:pager.previousUrl}<a href="{model:pager.previousUrl}" rel="prev">Previous</a>{/if}
            {foreach:model:pager.pages as pg}{if:pg:isCurrent}<span aria-current="page">{pg:number}</span>{else}<a href="{pg:url}">{pg:number}</a>{/if}{/foreach}
            {if:model:pager.nextUrl}<a href="{model:pager.nextUrl}" rel="next">Next</a>{/if}
            """, new { pager });

        Assert.Contains("<a href=\"/posts?q=%3Cb%3E\" rel=\"prev\">", html, StringComparison.Ordinal);
        Assert.Contains("<span aria-current=\"page\">2</span>", html, StringComparison.Ordinal);
        Assert.Contains("<a href=\"/posts?q=%3Cb%3E&amp;page=3\">3</a>", html, StringComparison.Ordinal);
        Assert.Contains("rel=\"next\"", html, StringComparison.Ordinal);
    }

    private static WebRequestContext Request(HttpContext http) => new(http, new WebRouteMetadata
    {
        Kind = WebRouteKind.Page,
        Pattern = "/",
        Methods = ["GET"],
        Contributor = new WebContributorDescriptor { Id = "test", Name = "Test" }
    }, JsonSerializerOptions.Default);
}

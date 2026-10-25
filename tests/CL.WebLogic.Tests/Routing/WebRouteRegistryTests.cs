using CL.WebLogic.Results;
using CL.WebLogic.Routing.Models;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Runtime.Services;

namespace CL.WebLogic.Tests.Routing;

public sealed class WebRouteRegistryTests
{
    private static readonly WebRouteHandler Handler = (_, _) => ValueTask.FromResult<WebResult>(WebResult.NoContent());

    private static WebRouteDefinition Route(string contributor, string pattern, WebRouteKind kind = WebRouteKind.Page, params string[] methods) => new()
    {
        Metadata = WebRouteMetadata.From(kind, pattern, methods.Length == 0 ? ["GET"] : methods,
            new WebContributorDescriptor { Id = contributor, Name = contributor }, new WebRouteSettings()),
        Handler = Handler,
        JsonOptions = WebJson.Default
    };

    [Fact]
    public void Add_ThenRemove_UpdatesSnapshotAndVersion()
    {
        using var registry = new WebRouteRegistry();
        var token = registry.GetChangeToken();

        registry.Add("a", [Route("a", "/x"), Route("a", "/y")]);

        Assert.Equal(2, registry.Routes.Count);
        Assert.True(token.HasChanged);
        Assert.True(registry.Remove("a"));
        Assert.Empty(registry.Routes);
        Assert.Equal(2, registry.Version);
    }

    [Theory]
    [InlineData("/items/{id}", "/items/{key}")]
    [InlineData("/Items", "/items")]
    [InlineData("/items/", "/items")]
    public void Add_RejectsAmbiguousRoutes(string first, string second)
    {
        using var registry = new WebRouteRegistry();
        registry.Add("a", [Route("a", first)]);

        Assert.Throws<InvalidOperationException>(() => registry.Add("b", [Route("b", second)]));
    }

    [Fact]
    public void Add_AllowsSamePatternWithDifferentMethods_AndConstraints()
    {
        using var registry = new WebRouteRegistry();
        registry.Add("a", [Route("a", "/items/{id}", WebRouteKind.Api, "GET")]);

        registry.Add("b", [Route("b", "/items/{id}", WebRouteKind.Api, "POST"), Route("b", "/items/{id:int}", WebRouteKind.Api, "GET")]);

        Assert.Equal(3, registry.Routes.Count);
    }

    [Fact]
    public void Add_RejectsSecondFallback()
    {
        using var registry = new WebRouteRegistry();
        registry.Add("a", [Route("a", "{**path}", WebRouteKind.Fallback)]);

        Assert.Throws<InvalidOperationException>(() => registry.Add("b", [Route("b", "{**path}", WebRouteKind.Fallback, "POST")]));
    }

    [Fact]
    public void FailedAdd_LeavesRegistryUnchanged()
    {
        using var registry = new WebRouteRegistry();
        registry.Add("a", [Route("a", "/x")]);

        Assert.Throws<InvalidOperationException>(() => registry.Add("b", [Route("b", "/ok"), Route("b", "/x")]));

        Assert.Single(registry.Routes);
        Assert.DoesNotContain("b", registry.ContributorIds);
    }
}

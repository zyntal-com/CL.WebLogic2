using CL.WebLogic.Results;
using CL.WebLogic.Routing.Models;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Testing.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CL.WebLogic.Tests.Routing;

public sealed class WebContributionBuilderTests
{
    private static readonly WebRouteHandler Handler = (_, _) => ValueTask.FromResult<WebResult>(WebResult.NoContent());

    private static async Task<IReadOnlyList<WebRouteDefinition>> BuildAsync(Action<WebContributionBuilder> contribute)
    {
        using var ctx = new FakeWebLogicContext();
        await using var services = new ServiceCollection().BuildServiceProvider();
        using var registry = new WebRouteRegistry();
        using var registrar = new ContributorRegistrar(registry, services, ctx);
        await registrar.RegisterAsync(new DelegateContributor(contribute), new WebContributorDescriptor { Id = "t", Name = "t" }, CancellationToken.None);
        return registry.Routes;
    }

    [Fact]
    public async Task Routes_AreSecureByDefault()
    {
        var routes = await BuildAsync(b => b.MapPage("/x", Handler));

        Assert.False(routes.Single().Metadata.AllowAnonymous);
        Assert.Equal(["GET"], routes.Single().Metadata.Methods);
    }

    [Fact]
    public async Task Group_SettingsAreInherited_AndRouteCanOverride()
    {
        var routes = await BuildAsync(b =>
        {
            var api = b.MapGroup("/api").AllowAnonymous().WithCors("public").WithTags("api");
            var admin = api.MapGroup("admin").RequireGroups("admin");
            api.MapApi("status", Handler);
            admin.MapApi("users/{id:int}", Handler, "get", "DELETE").WithTags("users");
            api.MapApi("secure", Handler).RequireAuthenticated();
        });

        var status = routes.Single(r => r.Metadata.Pattern == "/api/status").Metadata;
        Assert.True(status.AllowAnonymous);
        Assert.Equal("public", status.CorsPolicy);

        var users = routes.Single(r => r.Metadata.Pattern == "/api/admin/users/{id:int}").Metadata;
        Assert.False(users.AllowAnonymous);
        Assert.Equal(["admin"], users.RequiredGroups);
        Assert.Equal(["GET", "DELETE"], users.Methods);
        Assert.Equal(["api", "users"], users.Tags);
        Assert.Equal("public", users.CorsPolicy);

        Assert.False(routes.Single(r => r.Metadata.Pattern == "/api/secure").Metadata.AllowAnonymous);
    }

    [Fact]
    public async Task ThrowingContributor_RegistersNothing()
    {
        using var ctx = new FakeWebLogicContext();
        await using var services = new ServiceCollection().BuildServiceProvider();
        using var registry = new WebRouteRegistry();
        using var registrar = new ContributorRegistrar(registry, services, ctx);

        await Assert.ThrowsAsync<InvalidOperationException>(() => registrar.RegisterAsync(new DelegateContributor(b =>
        {
            b.MapPage("/x", Handler);
            throw new InvalidOperationException("boom");
        }), new WebContributorDescriptor { Id = "t", Name = "t" }, CancellationToken.None));

        Assert.Empty(registry.Routes);
        Assert.False(registrar.IsRegistered("t"));
    }

    [Fact]
    public async Task Fallback_IsNotAllowedInsideGroups()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => BuildAsync(b => b.MapGroup("/x").MapFallback(Handler)));
    }

    [Fact]
    public async Task OnRemove_CleanupsRun_WhenContributorIsRemoved()
    {
        using var ctx = new FakeWebLogicContext();
        await using var services = new ServiceCollection().BuildServiceProvider();
        using var registry = new WebRouteRegistry();
        using var registrar = new ContributorRegistrar(registry, services, ctx);
        var cleaned = false;

        var handle = await registrar.RegisterAsync(new DelegateContributor(b =>
        {
            b.MapPage("/x", Handler);
            b.OnRemove(() => { cleaned = true; return ValueTask.CompletedTask; });
        }), new WebContributorDescriptor { Id = "t", Name = "t" }, CancellationToken.None);

        await handle.DisposeAsync();

        Assert.True(cleaned);
        Assert.Empty(registry.Routes);
    }

    [Fact]
    public async Task StaleHandle_DoesNotRemoveNewerRegistration()
    {
        using var ctx = new FakeWebLogicContext();
        await using var services = new ServiceCollection().BuildServiceProvider();
        using var registry = new WebRouteRegistry();
        using var registrar = new ContributorRegistrar(registry, services, ctx);
        var descriptor = new WebContributorDescriptor { Id = "p", Name = "p" };

        var first = await registrar.RegisterAsync(new DelegateContributor(b => b.MapPage("/a", Handler)), descriptor, CancellationToken.None);
        await registrar.UnregisterAsync("p", CancellationToken.None);
        await registrar.RegisterAsync(new DelegateContributor(b => b.MapPage("/b", Handler)), descriptor, CancellationToken.None);

        await first.DisposeAsync();

        Assert.True(registrar.IsRegistered("p"));
        Assert.Equal("/b", registry.Routes.Single().Metadata.Pattern);
    }
}

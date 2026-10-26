using CL.WebLogic.Auth.Users;
using CL.WebLogic.Hosting.Services;
using CodeLogic.Core.Results;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CL.WebLogic.IntegrationTests.Auth;

/// <summary><c>web.Use*Store&lt;T&gt;()</c>: the app can inject its store by type and gets WebLogic's instance.</summary>
public sealed class StoreRegistrationTests
{
    [Fact]
    public async Task A_custom_user_store_is_one_singleton_for_its_type_and_the_interface()
    {
        await using var host = await AuthTestSite.StartAsync(web: b => b.UseUserStore<AppUserStore>());

        var byType = host.Services.GetRequiredService<AppUserStore>();
        Assert.Same(byType, host.Services.GetRequiredService<IWebUserStore>());
        byType.SetDisplayName("Alice");
        Assert.Equal("Alice", ((AppUserStore)host.Services.GetRequiredService<IWebUserStore>()).LastDisplayName);
    }

    /// <summary>A store with an app-specific method next to the contract.</summary>
    private sealed class AppUserStore : IWebUserStore
    {
        public Task<WebUser?> FindByIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<WebUser?>(null);

        public Task<WebUser?> FindByLoginAsync(string login, CancellationToken cancellationToken) => Task.FromResult<WebUser?>(null);

        public Task<Result> CreateAsync(WebUser user, CancellationToken cancellationToken) => Task.FromResult(Result.Success());

        public Task UpdateAsync(WebUser user, CancellationToken cancellationToken) => Task.CompletedTask;

        public string? LastDisplayName { get; private set; }

        public void SetDisplayName(string displayName) => LastDisplayName = displayName;
    }
}

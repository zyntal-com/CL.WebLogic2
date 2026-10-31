using CL.Data.Mapping;
using CL.WebLogic.Data.Models;
using CL.WebLogic.Data.Services;

namespace CL.WebLogic.Data.Tests;

/// <summary><see cref="WebDataTables.For"/>: the table lists migration tools script.</summary>
public sealed class WebDataTablesTests
{
    private static string[] Names(IReadOnlyList<CodeLogic.Core.Results.Result<EntityMap>> maps) =>
        [.. maps.Select(m => m.Value!.GetNames(NamingPolicy.AsIs).Table).Order(StringComparer.Ordinal)];

    [Fact]
    public void Lists_the_tables_of_the_chosen_stores_and_the_key_ring()
    {
        Assert.Equal(["wl_sessions"], Names(WebDataTables.For(WebDataStores.Sessions)));
        Assert.Equal(["wl_dataprotection_keys", "wl_sessions"], Names(WebDataTables.For(WebDataStores.Sessions, keyRing: true)));
        Assert.Equal(["wl_user_logins", "wl_users"], Names(WebDataTables.For(WebDataStores.Users)));

        var all = Names(WebDataTables.For(WebDataStores.All, keyRing: true));
        Assert.Equal(13, all.Length);
        Assert.All(all, name => Assert.StartsWith("wl_", name, StringComparison.Ordinal));
        Assert.DoesNotContain("wl_users", Names(WebDataTables.For(WebDataStores.All & ~WebDataStores.Users & ~WebDataStores.ApiKeys)));
    }
}
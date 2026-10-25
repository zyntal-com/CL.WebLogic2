using CL.WebLogic.Dashboard.Models;
using CL.WebLogic.Dashboard.Services;

namespace CL.WebLogic.Tests.Dashboard;

public sealed class DashboardLayoutTests
{
    private static WebDashboardItem Item(string id, string zone, int order) => new() { InstanceId = id, WidgetName = "w", Zone = zone, Order = order };

    [Theory]
    [InlineData("home", true)]
    [InlineData("user:abc.def-1_2", true)]
    [InlineData("../etc", false)]
    [InlineData("a..b", false)]
    [InlineData("a/b", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Keys_are_validated(string? key, bool valid) => Assert.Equal(valid, WebDashboardLayouts.IsValidKey(key));

    [Fact]
    public void Normalize_orders_zones_renumbers_and_dedupes()
    {
        var items = WebDashboardLayouts.Normalize([Item("b", "side", 5), Item("a", "main", 9), Item("c", "main", 1), Item("a", "side", 0), new WebDashboardItem { WidgetName = "x", Zone = "main", Order = 2 }], ["main", "side"]);

        Assert.Equal(["c", "a", "b"], items.Where(i => i.InstanceId.Length == 1).Select(i => i.InstanceId));
        Assert.Equal([0, 1, 2], items.Where(i => i.Zone == "main").Select(i => i.Order));
        Assert.Matches("^w[0-9a-f]{16}$", items.Single(i => i.WidgetName == "x").InstanceId);
        Assert.Equal(4, items.Count);
    }

    [Fact]
    public void Move_add_and_remove()
    {
        IReadOnlyList<WebDashboardItem> items = [Item("a", "main", 0), Item("b", "main", 1), Item("c", "side", 0)];

        items = WebDashboardLayouts.Move(items, "c", "main", 1);
        Assert.Equal(["a", "c", "b"], items.Where(i => i.Zone == "main").OrderBy(i => i.Order).Select(i => i.InstanceId));
        Assert.DoesNotContain(items, i => i.Zone == "side");

        items = WebDashboardLayouts.Add(items, "clock", "side");
        Assert.Equal("clock", items.Single(i => i.Zone == "side").WidgetName);

        items = WebDashboardLayouts.Remove(items, "a");
        Assert.Equal(["c", "b"], items.Where(i => i.Zone == "main").Select(i => i.InstanceId));
        Assert.Throws<ArgumentException>(() => WebDashboardLayouts.Move(items, "nope", "main", 0));
    }

    [Fact]
    public async Task In_memory_store_uses_optimistic_versions()
    {
        var store = new InMemoryDashboardLayoutStore();
        var layout = new WebDashboardLayout { OwnerKey = "u1", DashboardKey = "home", Items = [Item("a", "main", 0)] };

        var first = await store.SaveAsync(layout, 0, CancellationToken.None);
        Assert.Equal(1, first.Value!.Version);
        var stale = await store.SaveAsync(layout, 0, CancellationToken.None);
        Assert.Equal("weblogic.dashboard.conflict", stale.Error!.Code);
        Assert.Equal(2, (await store.SaveAsync(layout, 1, CancellationToken.None)).Value!.Version);
        Assert.Null(await store.GetAsync("u2", "home", CancellationToken.None));
    }
}

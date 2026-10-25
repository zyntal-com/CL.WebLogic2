using CL.WebLogic.Routing.Models;
using CL.WebLogic.Widgets.Models;
using CL.WebLogic.Widgets.Services;

namespace CL.WebLogic.Tests.Widgets;

public sealed class WidgetRegistryTests
{
    private static WebWidgetDefinition Widget(string name, string contributor = "app") => new()
    {
        Name = name,
        Contributor = new WebContributorDescriptor { Id = contributor, Name = contributor },
        Handler = (_, _) => ValueTask.FromResult(WebWidgetResult.Empty)
    };

    [Fact]
    public void Names_are_unique_and_removal_is_per_registration()
    {
        var registry = new WebWidgetRegistry();
        var first = Widget("clock");
        registry.Add(first);

        var error = Assert.Throws<InvalidOperationException>(() => registry.Add(Widget("clock", "plugin")));
        Assert.Contains("already registered by 'app'", error.Message);

        Assert.False(registry.Remove(Widget("clock")));
        Assert.Same(first, registry.Find("clock"));
        Assert.True(registry.Remove(first));
        Assert.Null(registry.Find("clock"));
    }

    [Theory]
    [InlineData("clock", true)]
    [InlineData("latest-posts.v2", true)]
    [InlineData("Clock", false)]
    [InlineData("-x", false)]
    [InlineData("a b", false)]
    public void Widget_names_are_validated(string name, bool valid) => Assert.Equal(valid, WebWidgetRegistry.IsValidName(name));

    [Fact]
    public void Placements_are_ordered_per_area()
    {
        var registry = new WebWidgetRegistry();
        var contributor = new WebContributorDescriptor { Id = "app", Name = "app" };
        var late = new WebWidgetPlacement { Area = "side", WidgetName = "b", Contributor = contributor, Order = 5 };
        registry.AddPlacement(late);
        registry.AddPlacement(new WebWidgetPlacement { Area = "side", WidgetName = "a", Contributor = contributor, Order = 1 });
        registry.AddPlacement(new WebWidgetPlacement { Area = "footer", WidgetName = "c", Contributor = contributor });

        Assert.Equal(["a", "b"], registry.PlacementsFor("SIDE").Select(p => p.WidgetName));
        Assert.True(registry.RemovePlacement(late));
        Assert.Equal(["a"], registry.PlacementsFor("side").Select(p => p.WidgetName));
    }
}

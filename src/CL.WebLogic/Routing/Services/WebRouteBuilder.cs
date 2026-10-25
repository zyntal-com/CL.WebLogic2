using CL.WebLogic.Routing.Models;

namespace CL.WebLogic.Routing.Services;

/// <summary>A route staged on a <see cref="WebContributionBuilder"/>; configure it fluently before commit.</summary>
public sealed class WebRouteBuilder : IWebRouteSettingsBuilder
{
    internal WebRouteBuilder(
        WebRouteKind kind,
        string pattern,
        IReadOnlyList<string> methods,
        WebRouteHandler handler,
        WebRouteGroupBuilder? group)
    {
        Kind = kind;
        Pattern = pattern;
        Methods = methods;
        Handler = handler;
        Group = group;
    }

    /// <summary>What the route produces.</summary>
    public WebRouteKind Kind { get; }

    /// <summary>The full route pattern (group prefixes already applied).</summary>
    public string Pattern { get; }

    /// <summary>The HTTP methods the route answers.</summary>
    public IReadOnlyList<string> Methods { get; }

    /// <inheritdoc/>
    public WebRouteSettings Settings { get; } = new();

    internal WebRouteHandler Handler { get; }

    internal WebRouteGroupBuilder? Group { get; }

    internal int Order { get; private set; }

    /// <summary>Sets the ASP.NET endpoint order (lower wins among equally specific patterns).</summary>
    public WebRouteBuilder WithOrder(int order)
    {
        Order = order;
        return this;
    }

    /// <summary>Resolves group inheritance into the final settings.</summary>
    internal WebRouteSettings ResolveSettings()
    {
        var resolved = new WebRouteSettings();
        resolved.InheritFrom(Settings);
        resolved.Name = Settings.Name;
        resolved.Description = Settings.Description;
        for (var group = Group; group is not null; group = group.Parent)
            resolved.InheritFrom(group.Settings);
        return resolved;
    }
}

using CL.WebLogic.Routing.Models;

namespace CL.WebLogic.Routing.Services;

/// <summary>A builder that carries <see cref="WebRouteSettings"/> — a single route or a route group.</summary>
public interface IWebRouteSettingsBuilder
{
    /// <summary>The settings being built.</summary>
    WebRouteSettings Settings { get; }
}

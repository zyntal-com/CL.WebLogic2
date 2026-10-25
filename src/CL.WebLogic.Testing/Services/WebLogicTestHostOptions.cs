using CL.WebLogic.Configuration.Models;
using CL.WebLogic.Hosting.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace CL.WebLogic.Testing.Services;

/// <summary>Options for <see cref="WebLogicTestHost"/>.</summary>
public sealed class WebLogicTestHostOptions
{
    /// <summary>The WebLogic config to start with (defaults when null).</summary>
    public WebLogicConfig? Config { get; set; }

    /// <summary>
    /// Permits the in-memory development stores whatever the environment (tests usually run
    /// without real stores). Default true; set false to test the production guard.
    /// </summary>
    public bool AllowDevelopmentStores { get; set; } = true;

    /// <summary>ASP.NET environment name. Defaults to Development.</summary>
    public string EnvironmentName { get; set; } = "Development";

    /// <summary>Content root (theme paths resolve against it). Defaults to a fresh temp folder.</summary>
    public string? ContentRootPath { get; set; }

    /// <summary>Configures WebLogic (contributors, stores, stages…).</summary>
    public Action<WebLogicBuilder>? ConfigureWebLogic { get; set; }

    /// <summary>Registers extra services.</summary>
    public Action<IServiceCollection>? ConfigureServices { get; set; }

    /// <summary>Adds extra middleware or endpoints (runs after <c>UseCodeLogicWebLogic</c>, before <c>MapCodeLogicWebLogic</c>).</summary>
    public Action<WebApplication>? ConfigureApp { get; set; }
}

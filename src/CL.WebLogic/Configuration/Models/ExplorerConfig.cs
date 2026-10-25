using CodeLogic.Core.Configuration;

namespace CL.WebLogic.Configuration.Models;

/// <summary>The API explorer at <c>/_weblogic/explorer</c>.</summary>
public sealed class ExplorerConfig
{
    /// <summary>Serve the explorer in the Development environment (anonymous, local tooling).</summary>
    [ConfigField(Label = "In Development", Group = "Explorer", Order = 1, RequiresRestart = true)]
    public bool InDevelopment { get; set; } = true;

    /// <summary>Serve the explorer outside Development (requires <see cref="Permission"/>).</summary>
    [ConfigField(Label = "In Production", Description = "Requires the explorer permission.", Group = "Explorer", Order = 2, RequiresRestart = true)]
    public bool InProduction { get; set; }

    /// <summary>Permission required outside Development.</summary>
    [ConfigField(Label = "Permission", Group = "Explorer", Order = 3, Collapsed = true)]
    public string Permission { get; set; } = "weblogic.explorer";
}

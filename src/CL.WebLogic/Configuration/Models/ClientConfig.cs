using CodeLogic.Core.Configuration;

namespace CL.WebLogic.Configuration.Models;

/// <summary>The WebLogic client runtime (<c>{weblogic_scripts}</c>).</summary>
public sealed class ClientConfig
{
    /// <summary>Emit the client from <c>{weblogic_scripts}</c>.</summary>
    [ConfigField(Label = "Enabled", Group = "Client", Order = 1)]
    public bool Enabled { get; set; } = true;

    /// <summary>UI adapter: <c>plain</c>, <c>bootstrap5</c> or <c>none</c> (the app installs its own).</summary>
    [ConfigField(Label = "UI Adapter", Description = "plain, bootstrap5 or none", Group = "Client", Order = 2)]
    public string UiAdapter { get; set; } = "plain";

    /// <summary><c>module</c> (ES module bundle) or <c>classic</c> (IIFE script).</summary>
    [ConfigField(Label = "Script Format", Description = "module or classic", Group = "Client", Order = 3, Collapsed = true)]
    public string Format { get; set; } = "module";

    /// <summary>Partial page navigation for pages with a <c>[data-wl-shell]</c> element.</summary>
    [ConfigField(Label = "Partial Navigation", Group = "Client", Order = 4)]
    public bool Navigation { get; set; } = true;

    /// <summary>Include <c>weblogic.css</c> (styles for the plain adapter).</summary>
    [ConfigField(Label = "Include Stylesheet", Group = "Client", Order = 5, Collapsed = true)]
    public bool IncludeStylesheet { get; set; } = true;

    /// <summary>Serve the client test page at <c>/_weblogic/tests</c> in the Development environment.</summary>
    [ConfigField(Label = "Test Page (Development)", Group = "Client", Order = 6, Collapsed = true)]
    public bool TestPageInDevelopment { get; set; } = true;
}

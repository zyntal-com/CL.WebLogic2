using CodeLogic.Framework.Application.Plugins;

namespace CL.WebLogic.Routing.Services;

/// <summary>Where loaded CodeLogic plugins come from. Abstracted so tests can supply their own.</summary>
public interface IWebPluginSource
{
    /// <summary>Plugins loaded right now.</summary>
    IReadOnlyList<IPlugin> GetLoadedPlugins();

    /// <summary>A loaded plugin by id, or null.</summary>
    IPlugin? GetPlugin(string pluginId);
}

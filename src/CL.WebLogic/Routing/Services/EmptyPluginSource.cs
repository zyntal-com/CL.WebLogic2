using CodeLogic.Framework.Application.Plugins;

namespace CL.WebLogic.Routing.Services;

/// <summary>A plugin source with no plugins — used when WebLogic runs without the CodeLogic runtime (tests).</summary>
public sealed class EmptyPluginSource : IWebPluginSource
{
    /// <inheritdoc/>
    public IReadOnlyList<IPlugin> GetLoadedPlugins() => [];

    /// <inheritdoc/>
    public IPlugin? GetPlugin(string pluginId) => null;
}

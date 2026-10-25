using CodeLogic.Framework.Application.Plugins;

namespace CL.WebLogic.Routing.Services;

/// <summary>Reads plugins from the CodeLogic <see cref="PluginManager"/> (empty when none is set).</summary>
public sealed class CodeLogicPluginSource : IWebPluginSource
{
    /// <inheritdoc/>
    public IReadOnlyList<IPlugin> GetLoadedPlugins() =>
        CodeLogic.CodeLogic.GetPluginManager()?.GetAllPlugins().ToList() ?? [];

    /// <inheritdoc/>
    public IPlugin? GetPlugin(string pluginId) =>
        CodeLogic.CodeLogic.GetPluginManager()?.GetPlugin<IPlugin>(pluginId);
}

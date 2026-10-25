using CodeLogic;

namespace CL.WebLogic;

/// <summary>
/// Loads the CodeLogic libraries WebLogic builds on, then <see cref="WebLogicLibrary"/> itself.
/// Call between <c>CodeLogic.InitializeAsync()</c> and <c>CodeLogic.ConfigureAsync()</c>.
/// </summary>
public static class WebLogicBootstrap
{
    /// <summary>Loads WebLogic and the libraries it integrates with.</summary>
    /// <returns>True when every library loaded.</returns>
    public static async Task<bool> LoadAsync()
    {
        return await Libraries.LoadAsync<WebLogicLibrary>().ConfigureAwait(false);
    }
}

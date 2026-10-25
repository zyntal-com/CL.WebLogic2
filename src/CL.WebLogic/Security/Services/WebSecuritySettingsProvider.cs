using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Security.Models;

namespace CL.WebLogic.Security.Services;

/// <summary>Hands out the <see cref="WebSecuritySettings"/> for the current config revision, recompiling on change.</summary>
public sealed class WebSecuritySettingsProvider
{
    private readonly WebLogicConfigHolder _config;
    private readonly object _lock = new();
    private WebSecuritySettings? _current;

    /// <summary>Creates the provider.</summary>
    public WebSecuritySettingsProvider(WebLogicConfigHolder config) =>
        _config = config ?? throw new ArgumentNullException(nameof(config));

    /// <summary>Settings for the current config.</summary>
    public WebSecuritySettings Current
    {
        get
        {
            var revision = _config.Revision;
            var current = Volatile.Read(ref _current);
            if (current is not null && current.Revision == revision)
                return current;

            lock (_lock)
            {
                current = _current;
                if (current is null || current.Revision != revision)
                {
                    current = WebSecuritySettings.Compile(_config.Current, revision);
                    Volatile.Write(ref _current, current);
                }
                return current;
            }
        }
    }
}

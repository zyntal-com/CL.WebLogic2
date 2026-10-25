using CL.WebLogic.Configuration.Services;
using CodeLogic.Core.Events;
using CodeLogic.Core.Logging;
using CodeLogic.Framework.Libraries;

namespace CL.WebLogic.Hosting.Services;

/// <summary>Adapts a CodeLogic <see cref="LibraryContext"/> to <see cref="IWebLogicContext"/>.</summary>
public sealed class LibraryWebLogicContext : IWebLogicContext
{
    private readonly LibraryContext _library;
    private readonly Func<Task<HealthStatus>> _health;

    /// <summary>Creates the adapter over the WebLogic library's context.</summary>
    public LibraryWebLogicContext(LibraryContext library, WebLogicConfigHolder config, Func<Task<HealthStatus>> health)
    {
        _library = library ?? throw new ArgumentNullException(nameof(library));
        Config = config ?? throw new ArgumentNullException(nameof(config));
        _health = health ?? throw new ArgumentNullException(nameof(health));
    }

    /// <inheritdoc/>
    public ILogger Logger => _library.Logger;

    /// <inheritdoc/>
    public IEventBus Events => _library.Events;

    /// <inheritdoc/>
    public WebLogicConfigHolder Config { get; }

    /// <inheritdoc/>
    public string DataDirectory => _library.DataDirectory;

    /// <inheritdoc/>
    public Task<HealthStatus> CheckHealthAsync(CancellationToken cancellationToken) => _health();
}

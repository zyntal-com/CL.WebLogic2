using CL.WebLogic.Configuration.Models;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Hosting.Services;
using CodeLogic.Core.Events;
using CodeLogic.Core.Logging;
using CodeLogic.Framework.Libraries;

namespace CL.WebLogic.Testing.Services;

/// <summary>
/// An <see cref="IWebLogicContext"/> for tests: a recording logger, a private event bus and a
/// temporary data directory. Needs no CodeLogic runtime, so tests can run in parallel.
/// </summary>
public sealed class FakeWebLogicContext : IWebLogicContext, IDisposable
{
    /// <summary>Creates a context around <paramref name="config"/> (defaults when null).</summary>
    public FakeWebLogicContext(WebLogicConfig? config = null)
    {
        Config = new WebLogicConfigHolder(config ?? new WebLogicConfig());
        DataDirectory = Path.Combine(Path.GetTempPath(), "weblogic-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(DataDirectory);
    }

    /// <summary>The recording logger, for assertions.</summary>
    public RecordingLogger Log { get; } = new();

    /// <inheritdoc/>
    public ILogger Logger => Log;

    /// <inheritdoc/>
    public IEventBus Events { get; } = new EventBus();

    /// <inheritdoc/>
    public WebLogicConfigHolder Config { get; }

    /// <inheritdoc/>
    public string DataDirectory { get; }

    /// <summary>Health reported by <see cref="CheckHealthAsync"/>; healthy by default.</summary>
    public HealthStatus Health { get; set; } = HealthStatus.Healthy("Fake WebLogic context");

    /// <inheritdoc/>
    public Task<HealthStatus> CheckHealthAsync(CancellationToken cancellationToken) => Task.FromResult(Health);

    /// <summary>Deletes the temporary data directory.</summary>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(DataDirectory))
                Directory.Delete(DataDirectory, recursive: true);
        }
        catch (IOException)
        {
            // A file still held open by a test — the OS temp cleaner will get it.
        }
    }
}

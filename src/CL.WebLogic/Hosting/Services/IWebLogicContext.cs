using CL.WebLogic.Configuration.Services;
using CodeLogic.Core.Events;
using CodeLogic.Core.Logging;
using CodeLogic.Framework.Libraries;

namespace CL.WebLogic.Hosting.Services;

/// <summary>
/// The slice of the CodeLogic runtime that WebLogic services depend on. Core services take
/// this instead of <c>LibraryContext</c>, so they can be constructed in tests without the
/// static CodeLogic runtime (see <c>CodeLogic.WebLogic.Testing</c>).
/// </summary>
public interface IWebLogicContext
{
    /// <summary>Logger scoped to the WebLogic library.</summary>
    ILogger Logger { get; }

    /// <summary>The shared CodeLogic event bus.</summary>
    IEventBus Events { get; }

    /// <summary>Holder of the current WebLogic config.</summary>
    WebLogicConfigHolder Config { get; }

    /// <summary>Writable data directory owned by the WebLogic library.</summary>
    string DataDirectory { get; }

    /// <summary>Health of the WebLogic library (folded into the ASP.NET readiness check).</summary>
    Task<HealthStatus> CheckHealthAsync(CancellationToken cancellationToken);
}

using CL.WebLogic.Configuration.Services;
using Microsoft.Extensions.Hosting;

namespace CL.WebLogic.Hosting.Services;

/// <summary>
/// With <c>Cluster.Enabled</c>, refuses to start while shared state is kept per process (see
/// <see cref="ClusterReadinessCheck"/>). Warnings are logged; errors stop the host.
/// </summary>
public sealed class ClusterGuard : IHostedService
{
    private readonly ClusterReadinessCheck _check;
    private readonly WebLogicConfigHolder _config;
    private readonly IWebLogicContext _context;

    /// <summary>Creates the guard.</summary>
    public ClusterGuard(ClusterReadinessCheck check, WebLogicConfigHolder config, IWebLogicContext context)
    {
        _check = check ?? throw new ArgumentNullException(nameof(check));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_config.Current.Cluster.Enabled)
            return Task.CompletedTask;

        var report = _check.Evaluate();
        foreach (var warning in report.Warnings)
            _context.Logger.Warning("Cluster: " + warning);
        if (!report.IsReady)
            throw new InvalidOperationException("Cluster.Enabled is set, but this node keeps shared state in the process:"
                + Environment.NewLine + string.Join(Environment.NewLine, report.Errors.Select(e => " - " + e)));

        _context.Logger.Info("Cluster: shared state is stored outside the process.");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

using CL.WebLogic.Configuration.Events;
using CL.WebLogic.Configuration.Models;
using CodeLogic.Core.Configuration;
using CodeLogic.Core.Events;
using CodeLogic.Core.Logging;

namespace CL.WebLogic.Configuration.Services;

/// <summary>
/// Applies a live <see cref="WebLogicConfig"/> change: diffs old and new, flags restart-only
/// fields, swaps the <see cref="WebLogicConfigHolder"/> and publishes the outcome.
/// <para>
/// Restart-only changes never throw — they are stored, logged and reported through
/// <see cref="RestartPending"/> and <see cref="WebLogicRestartRequiredEvent"/>. Only a
/// genuine failure to apply propagates, which makes CodeLogic report the library Degraded.
/// </para>
/// </summary>
public sealed class ConfigApplyCoordinator
{
    private readonly WebLogicConfigHolder _holder;
    private readonly ILogger _logger;
    private readonly IEventBus _events;
    private volatile bool _restartPending;

    /// <summary>Creates the coordinator.</summary>
    public ConfigApplyCoordinator(WebLogicConfigHolder holder, ILogger logger, IEventBus events)
    {
        _holder = holder ?? throw new ArgumentNullException(nameof(holder));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _events = events ?? throw new ArgumentNullException(nameof(events));
    }

    /// <summary>True once a change touched a restart-only field since startup.</summary>
    public bool RestartPending => _restartPending;

    /// <summary>Applies <paramref name="next"/> as the new config.</summary>
    /// <returns>Dotted paths of the properties that changed.</returns>
    public async Task<IReadOnlyList<string>> ApplyAsync(WebLogicConfig next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);
        cancellationToken.ThrowIfCancellationRequested();

        var previous = _holder.Current;
        var changed = ConfigDiff.ChangedProperties(previous, next);
        if (changed.Count == 0)
            return changed;

        var restartOnly = changed
            .Where(path => ConfigDiff.RequiresRestart(typeof(WebLogicConfig), [path]))
            .ToList();

        if (restartOnly.Count > 0)
        {
            _restartPending = true;
            _logger.Warning($"WebLogic config change requires a restart to take full effect: {string.Join(", ", restartOnly)}");
            await _events.PublishAsync(new WebLogicRestartRequiredEvent(restartOnly)).ConfigureAwait(false);
        }

        _holder.Swap(next);
        _logger.Info($"WebLogic config applied ({changed.Count} change(s)): {string.Join(", ", changed)}");
        await _events.PublishAsync(new WebLogicConfigAppliedEvent(changed)).ConfigureAwait(false);
        return changed;
    }
}

using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Security.Services;

namespace CL.WebLogic.Security.Pipeline;

/// <summary>The validated, ordered stages per phase.</summary>
public sealed class WebSecurityPipeline
{
    private readonly IWebSecurityStage[] _preRouting;
    private readonly IWebSecurityStage[] _endpoint;
    private readonly HashSet<string> _disabled;

    /// <summary>Creates the pipeline; throws when the stages are invalid (duplicate names, wrong order).</summary>
    public WebSecurityPipeline(IEnumerable<IWebSecurityStage> stages, WebSecuritySettingsProvider settings, IWebLogicContext context)
    {
        ArgumentNullException.ThrowIfNull(stages);
        ArgumentNullException.ThrowIfNull(settings);
        // Fixed at startup (Security.DisabledStages requires a restart): a live config reload must
        // never be able to switch off authorization or antiforgery silently.
        _disabled = new HashSet<string>(settings.Current.DisabledStages, StringComparer.OrdinalIgnoreCase);
        var all = stages.ToList();

        var errors = StageOrderValidator.Validate(all);
        if (errors.Count > 0)
            throw new InvalidOperationException("Invalid security pipeline: " + string.Join(" ", errors));

        _preRouting = [.. all.Where(s => s.Phase == WebSecurityPhase.PreRouting).OrderBy(s => s.Order)];
        _endpoint = [.. all.Where(s => s.Phase == WebSecurityPhase.Endpoint).OrderBy(s => s.Order)];

        foreach (var disabled in settings.Current.DisabledStages)
            context.Logger.Warning($"Security stage '{disabled}' is disabled by configuration.");
    }

    /// <summary>All stages in execution order (for the explorer).</summary>
    public IReadOnlyList<IWebSecurityStage> Stages => [.. _preRouting, .. _endpoint];

    /// <summary>The enabled stages of <paramref name="phase"/>, in order.</summary>
    public IEnumerable<IWebSecurityStage> GetStages(WebSecurityPhase phase)
    {
        foreach (var stage in phase == WebSecurityPhase.PreRouting ? _preRouting : _endpoint)
        {
            if (!_disabled.Contains(stage.Name))
                yield return stage;
        }
    }
}

namespace CL.WebLogic.Security.Pipeline;

/// <summary>
/// Checks the registered stages at startup: unique names, and the built-in stages in their
/// required relative order (e.g. authentication before authorization, authorization before CSRF).
/// A broken pipeline fails startup instead of silently skipping a check.
/// </summary>
public static class StageOrderValidator
{
    /// <summary>The canonical order of built-in stages (phase-major).</summary>
    public static IReadOnlyList<string> CanonicalOrder { get; } =
    [
        "forwarded-headers", "host-filter", "https", "tenant", "ip-rules", "reputation", "global-rate-limit", "authentication",
        "request-limits", "route-rate-limit", "authorization", "antiforgery"
    ];

    private static readonly Dictionary<string, WebSecurityPhase> RequiredPhases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["forwarded-headers"] = WebSecurityPhase.PreRouting,
        ["authentication"] = WebSecurityPhase.PreRouting,
        ["authorization"] = WebSecurityPhase.Endpoint,
        ["antiforgery"] = WebSecurityPhase.Endpoint
    };

    /// <summary>Returns the problems found (empty when valid).</summary>
    public static IReadOnlyList<string> Validate(IEnumerable<IWebSecurityStage> stages)
    {
        var list = stages.ToList();
        var errors = new List<string>();

        foreach (var duplicate in list.GroupBy(s => s.Name, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
            errors.Add($"Security stage name '{duplicate.Key}' is registered {duplicate.Count()} times.");

        foreach (var stage in list)
        {
            if (string.IsNullOrWhiteSpace(stage.Name))
                errors.Add($"Security stage {stage.GetType().Name} has no name.");
            if (RequiredPhases.TryGetValue(stage.Name ?? string.Empty, out var phase) && stage.Phase != phase)
                errors.Add($"Security stage '{stage.Name}' must run in phase {phase}.");
        }

        var builtIn = list
            .Where(s => CanonicalOrder.Contains(s.Name, StringComparer.OrdinalIgnoreCase))
            .OrderBy(s => s.Phase).ThenBy(s => s.Order)
            .Select(s => s.Name)
            .ToList();
        var expected = CanonicalOrder.Where(n => builtIn.Contains(n, StringComparer.OrdinalIgnoreCase)).ToList();
        if (!builtIn.SequenceEqual(expected, StringComparer.OrdinalIgnoreCase))
            errors.Add($"Security stages are out of order: [{string.Join(", ", builtIn)}]; required: [{string.Join(", ", expected)}].");

        return errors;
    }
}

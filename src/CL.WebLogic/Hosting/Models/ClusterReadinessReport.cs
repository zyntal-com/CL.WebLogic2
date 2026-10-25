namespace CL.WebLogic.Hosting.Models;

/// <summary>What stops (errors) or weakens (warnings) running this app as several nodes.</summary>
public sealed record ClusterReadinessReport(IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings)
{
    /// <summary>True when nothing blocks clustering.</summary>
    public bool IsReady => Errors.Count == 0;
}

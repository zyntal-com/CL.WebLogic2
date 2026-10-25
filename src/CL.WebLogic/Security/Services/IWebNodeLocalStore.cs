namespace CL.WebLogic.Security.Services;

/// <summary>
/// Marks an implementation that keeps its state in this process only (the in-memory defaults).
/// Fine for one node; with <c>Cluster.Enabled</c> the app refuses to start while one backs a
/// contract that must be shared.
/// </summary>
public interface IWebNodeLocalStore
{
}

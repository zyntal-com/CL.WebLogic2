namespace CL.WebLogic.Security.Services;

/// <summary>
/// Marks an in-memory development implementation of a security store. Outside Development the
/// app refuses to start while one is registered, unless <c>Security.AllowDevelopmentStores</c> is set.
/// </summary>
public interface IWebDevelopmentStore
{
}

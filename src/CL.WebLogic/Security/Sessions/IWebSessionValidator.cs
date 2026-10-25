using CL.WebLogic.Security.Pipeline;

namespace CL.WebLogic.Security.Sessions;

/// <summary>Checked for every signed-in session; returning false signs the session out.</summary>
public interface IWebSessionValidator
{
    /// <summary>True when <paramref name="session"/> may stay signed in.</summary>
    ValueTask<bool> ValidateAsync(LazyWebSession session, WebSecurityContext context, CancellationToken cancellationToken);
}

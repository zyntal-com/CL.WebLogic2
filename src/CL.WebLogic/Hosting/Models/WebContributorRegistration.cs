using CL.WebLogic.Routing;
using CL.WebLogic.Routing.Models;

namespace CL.WebLogic.Hosting.Models;

/// <summary>An application contributor registered through <c>web.AddContributor</c>, run at host start.</summary>
public sealed class WebContributorRegistration
{
    /// <summary>Contributor type resolved from DI (when <see cref="Instance"/> is null).</summary>
    public required Type ContributorType { get; init; }

    /// <summary>A pre-built contributor instance, or null to resolve <see cref="ContributorType"/>.</summary>
    public IWebContributor? Instance { get; init; }

    /// <summary>Descriptor override, or null to derive one from the type.</summary>
    public WebContributorDescriptor? Descriptor { get; init; }
}

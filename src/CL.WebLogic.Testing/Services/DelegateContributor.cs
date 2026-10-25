using CL.WebLogic.Routing;
using CL.WebLogic.Routing.Services;

namespace CL.WebLogic.Testing.Services;

/// <summary>An <see cref="IWebContributor"/> built from a lambda — handy in tests.</summary>
public sealed class DelegateContributor : IWebContributor
{
    private readonly Action<WebContributionBuilder> _contribute;

    /// <summary>Creates the contributor.</summary>
    public DelegateContributor(Action<WebContributionBuilder> contribute) =>
        _contribute = contribute ?? throw new ArgumentNullException(nameof(contribute));

    /// <inheritdoc/>
    public Task ContributeAsync(WebContributionBuilder builder, CancellationToken cancellationToken)
    {
        _contribute(builder);
        return Task.CompletedTask;
    }
}

using CL.WebLogic.Security.Pipeline;
using CL.WebLogic.Security.Tenancy;

namespace CL.WebLogic.Security.Stages;

/// <summary>Sets <see cref="WebSecurityContext.TenantId"/> from the registered <see cref="IWebTenantResolver"/> (no-op without one).</summary>
public sealed class TenantStage : IWebSecurityStage
{
    private readonly IWebTenantResolver? _resolver;

    /// <summary>Creates the stage.</summary>
    public TenantStage(IEnumerable<IWebTenantResolver> resolvers) => _resolver = resolvers?.LastOrDefault();

    /// <inheritdoc/>
    public string Name => "tenant";

    /// <inheritdoc/>
    public WebSecurityPhase Phase => WebSecurityPhase.PreRouting;

    /// <inheritdoc/>
    public int Order => 250;

    /// <inheritdoc/>
    public async ValueTask<WebSecurityDecision> ExecuteAsync(WebSecurityContext context, CancellationToken cancellationToken)
    {
        if (_resolver is not null)
            context.TenantId = await _resolver.ResolveAsync(context, cancellationToken).ConfigureAwait(false);
        return WebSecurityDecision.Continue;
    }
}

using CL.WebLogic.Security.Pipeline;
using CL.WebLogic.Security.RateLimiting;

namespace CL.WebLogic.Security.Stages;

/// <summary>Site-wide per-IP token bucket (<c>RateLimits.GlobalPermitsPerMinute</c> + burst).</summary>
public sealed class GlobalRateLimitStage : IWebSecurityStage
{
    private readonly IWebRateLimitStore _store;

    /// <summary>Creates the stage.</summary>
    public GlobalRateLimitStage(IWebRateLimitStore store) => _store = store ?? throw new ArgumentNullException(nameof(store));

    /// <inheritdoc/>
    public string Name => "global-rate-limit";

    /// <inheritdoc/>
    public WebSecurityPhase Phase => WebSecurityPhase.PreRouting;

    /// <inheritdoc/>
    public int Order => 400;

    /// <inheritdoc/>
    public async ValueTask<WebSecurityDecision> ExecuteAsync(WebSecurityContext context, CancellationToken cancellationToken)
    {
        if (!context.Settings.Config.RateLimits.GlobalEnabled || context.ClientIp is null)
            return WebSecurityDecision.Continue;

        var rule = context.Settings.GlobalRateLimit;
        var decision = await _store.TryAcquireAsync("global:" + ClientAddressKey.For(context.ClientIp, context.Settings.Config.RateLimits.Ipv6PrefixLength), rule, cancellationToken).ConfigureAwait(false);
        return decision.Allowed
            ? WebSecurityDecision.Continue
            : WebSecurityDecision.ShortCircuit(RateLimitResponses.TooManyRequests(context.HttpContext, decision), "global-rate-limit");
    }
}

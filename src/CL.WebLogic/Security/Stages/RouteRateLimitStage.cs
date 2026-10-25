using CL.WebLogic.Configuration.Models;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Security.Identity;
using CL.WebLogic.Security.Pipeline;
using CL.WebLogic.Security.RateLimiting;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Security.Stages;

/// <summary>
/// Applies the named policy of the route (<c>.WithRateLimit("login")</c>), partitioned by IP, user,
/// API key, route or tenant. An unknown policy name fails closed (500) so a typo cannot silently
/// remove protection.
/// </summary>
public sealed class RouteRateLimitStage : IWebSecurityStage
{
    private readonly IWebRateLimitStore _store;
    private readonly IWebLogicContext _context;

    /// <summary>Creates the stage.</summary>
    public RouteRateLimitStage(IWebRateLimitStore store, IWebLogicContext context)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc/>
    public string Name => "route-rate-limit";

    /// <inheritdoc/>
    public WebSecurityPhase Phase => WebSecurityPhase.Endpoint;

    /// <inheritdoc/>
    public int Order => 650;

    /// <inheritdoc/>
    public async ValueTask<WebSecurityDecision> ExecuteAsync(WebSecurityContext context, CancellationToken cancellationToken)
    {
        // A per-credential policy (e.g. an API key's) applies in addition to the route's.
        if (context.Identity.Claims.TryGetValue(WebIdentityClaims.RateLimitPolicy, out var credentialPolicy) && credentialPolicy.Length > 0)
        {
            var credentialDecision = await ApplyAsync(context, credentialPolicy, RateLimitPartition.ApiKey, cancellationToken).ConfigureAwait(false);
            if (credentialDecision is not null)
                return credentialDecision;
        }

        var policyName = context.Route?.RateLimitPolicy;
        if (string.IsNullOrEmpty(policyName))
            return WebSecurityDecision.Continue;
        return await ApplyAsync(context, policyName, null, cancellationToken).ConfigureAwait(false) ?? WebSecurityDecision.Continue;
    }

    private async ValueTask<WebSecurityDecision?> ApplyAsync(WebSecurityContext context, string policyName, RateLimitPartition? partition, CancellationToken cancellationToken)
    {
        if (!context.Settings.RateLimitPolicies.TryGetValue(policyName, out var rule))
        {
            _context.Logger.Error($"Rate-limit policy '{policyName}' (route '{context.Route?.Pattern}') is not defined. Add it to Security.RateLimits.Policies.");
            return WebSecurityDecision.Reject(StatusCodes.Status500InternalServerError, "unknown-rate-limit-policy");
        }

        var key = rule.Name + ":" + PartitionKey(context, partition ?? rule.Partition);
        var decision = await _store.TryAcquireAsync(key, rule, cancellationToken).ConfigureAwait(false);
        if (!decision.Allowed)
            return WebSecurityDecision.ShortCircuit(RateLimitResponses.TooManyRequests(context.HttpContext, decision), "route-rate-limit:" + rule.Name);

        foreach (var (name, value) in RateLimitResponses.Headers(decision))
            context.HttpContext.Response.Headers[name] = value;
        return null;
    }

    private static string PartitionKey(WebSecurityContext context, RateLimitPartition partition)
    {
        var identity = context.Identity;
        var ip = "ip:" + ClientAddressKey.For(context.ClientIp, context.Settings.Config.RateLimits.Ipv6PrefixLength);
        return partition switch
        {
            RateLimitPartition.Route => "route:" + context.Route!.Pattern,
            RateLimitPartition.Tenant => context.TenantId is { } tenant ? "tenant:" + tenant : ip,
            RateLimitPartition.ApiKey when identity.Claims.TryGetValue(WebIdentityClaims.KeyId, out var keyId) => "key:" + keyId,
            RateLimitPartition.ApiKey when identity.Scheme is WebAuthenticationSchemes.ApiKey or WebAuthenticationSchemes.Bearer => "key:" + identity.UserId,
            RateLimitPartition.User or RateLimitPartition.ApiKey when identity.IsAuthenticated => "user:" + identity.UserId,
            _ => ip
        };
    }
}

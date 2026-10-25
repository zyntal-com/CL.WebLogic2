using CL.NetUtils;
using CL.WebLogic.Configuration.Models;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Security.Pipeline;
using CL.WebLogic.Security.Services;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Security.Stages;

/// <summary>
/// DNS blocklist checks through CL.NetUtils with a bounded verdict cache. Blocking mode waits up
/// to the timeout; Background mode never delays a request (a listed address is refused from its
/// next request). Lookup errors never block (fail open) and are cached briefly.
/// </summary>
public sealed class ReputationStage : IWebSecurityStage, IDisposable
{
    private readonly SemaphoreSlim _backgroundSlots = new(16, 16);
    private readonly IWebLibraryResolver _libraries;
    private readonly IWebLogicContext _context;
    private readonly TimeProvider? _time;
    private BoundedTtlCache<bool>? _verdicts;

    /// <summary>Creates the stage.</summary>
    public ReputationStage(IWebLibraryResolver libraries, IWebLogicContext context, TimeProvider? time = null)
    {
        _libraries = libraries ?? throw new ArgumentNullException(nameof(libraries));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _time = time;
    }

    /// <inheritdoc/>
    public string Name => "reputation";

    /// <inheritdoc/>
    public WebSecurityPhase Phase => WebSecurityPhase.PreRouting;

    /// <inheritdoc/>
    public int Order => 350;

    /// <inheritdoc/>
    public async ValueTask<WebSecurityDecision> ExecuteAsync(WebSecurityContext context, CancellationToken cancellationToken)
    {
        var config = context.Settings.Config.Reputation;
        if (!config.Enabled || context.ClientIp is null)
            return WebSecurityDecision.Continue;

        var ip = context.ClientIp.ToString();
        var cache = _verdicts ??= new BoundedTtlCache<bool>(config.CacheSize, _time);
        if (cache.TryGet(ip, out var listed))
            return Verdict(listed);

        var netUtils = _libraries.GetLibrary<NetUtilsLibrary>();
        if (netUtils is null)
            return WebSecurityDecision.Continue;

        if (config.Mode == ReputationMode.Background)
        {
            if (_backgroundSlots.Wait(0, CancellationToken.None))
                _ = CheckInBackgroundAsync(netUtils, ip, config);
            return WebSecurityDecision.Continue;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(config.TimeoutMilliseconds);
        try
        {
            return Verdict(await CheckAsync(netUtils, ip, config, timeout.Token).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return WebSecurityDecision.Continue; // over budget: let it through, the lookup is not cached
        }
    }

    /// <inheritdoc/>
    public void Dispose() => _backgroundSlots.Dispose();

    private static WebSecurityDecision Verdict(bool listed) =>
        listed ? WebSecurityDecision.Reject(StatusCodes.Status403Forbidden, "dnsbl-listed") : WebSecurityDecision.Continue;

    private async Task CheckInBackgroundAsync(NetUtilsLibrary netUtils, string ip, ReputationConfig config)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await CheckAsync(netUtils, ip, config, timeout.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _context.Logger.Debug($"Background DNSBL check failed: {ex.Message}");
        }
        finally
        {
            _backgroundSlots.Release();
        }
    }

    private async Task<bool> CheckAsync(NetUtilsLibrary netUtils, string ip, ReputationConfig config, CancellationToken cancellationToken)
    {
        var result = await netUtils.Dnsbl.CheckIpAsync(ip, cancellationToken).ConfigureAwait(false);
        var ttl = result.ErrorMessage is null ? TimeSpan.FromMinutes(config.CacheMinutes) : TimeSpan.FromMinutes(Math.Max(1, config.CacheMinutes / 10));
        _verdicts?.Set(ip, result.IsBlacklisted, ttl);
        if (result.IsBlacklisted)
            _context.Logger.Info($"Client listed on DNSBL {result.MatchedService}");
        return result.IsBlacklisted;
    }
}

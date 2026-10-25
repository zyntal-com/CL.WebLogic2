using CL.NetUtils;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Security.Pipeline;
using CL.WebLogic.Security.Services;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Security.Stages;

/// <summary>
/// Allow / deny by address and network, plus optional country rules through CL.NetUtils GeoIP
/// (cached). Addresses GeoIP cannot place (private, unknown) are not country-filtered.
/// </summary>
public sealed class IpRulesStage : IWebSecurityStage
{
    private readonly IWebLibraryResolver _libraries;
    private readonly IWebLogicContext _context;
    private readonly BoundedTtlCache<string> _countries;
    private int _warnedNoGeoIp;

    /// <summary>Creates the stage.</summary>
    public IpRulesStage(IWebLibraryResolver libraries, IWebLogicContext context, TimeProvider? time = null)
    {
        _libraries = libraries ?? throw new ArgumentNullException(nameof(libraries));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _countries = new BoundedTtlCache<string>(50_000, time);
    }

    /// <inheritdoc/>
    public string Name => "ip-rules";

    /// <inheritdoc/>
    public WebSecurityPhase Phase => WebSecurityPhase.PreRouting;

    /// <inheritdoc/>
    public int Order => 300;

    /// <inheritdoc/>
    public async ValueTask<WebSecurityDecision> ExecuteAsync(WebSecurityContext context, CancellationToken cancellationToken)
    {
        var settings = context.Settings;
        if (IpNetworkParser.ContainsAny(settings.DenyNetworks, context.ClientIp))
            return WebSecurityDecision.Reject(StatusCodes.Status403Forbidden, "ip-denied");
        if (settings.AllowNetworks.Count > 0 && !IpNetworkParser.ContainsAny(settings.AllowNetworks, context.ClientIp))
            return WebSecurityDecision.Reject(StatusCodes.Status403Forbidden, "ip-not-allowed");

        var rules = settings.Config.IpRules;
        if ((rules.DenyCountries.Count == 0 && rules.AllowCountries.Count == 0) || context.ClientIp is null)
            return WebSecurityDecision.Continue;

        var country = await LookupCountryAsync(context.ClientIp.ToString(), cancellationToken).ConfigureAwait(false);
        if (country.Length == 0)
            return WebSecurityDecision.Continue;
        if (rules.DenyCountries.Contains(country, StringComparer.OrdinalIgnoreCase))
            return WebSecurityDecision.Reject(StatusCodes.Status403Forbidden, "country-denied:" + country);
        if (rules.AllowCountries.Count > 0 && !rules.AllowCountries.Contains(country, StringComparer.OrdinalIgnoreCase))
            return WebSecurityDecision.Reject(StatusCodes.Status403Forbidden, "country-not-allowed:" + country);
        return WebSecurityDecision.Continue;
    }

    private async ValueTask<string> LookupCountryAsync(string ip, CancellationToken cancellationToken)
    {
        if (_countries.TryGet(ip, out var cached))
            return cached;

        var netUtils = _libraries.GetLibrary<NetUtilsLibrary>();
        if (netUtils is null || !netUtils.HasGeoIp)
        {
            if (Interlocked.Exchange(ref _warnedNoGeoIp, 1) == 0)
                _context.Logger.Warning("Country rules are configured but CL.NetUtils GeoIP is not available; country rules are skipped.");
            return string.Empty;
        }

        var result = await netUtils.GeoIp.LookupIpAsync(ip, cancellationToken).ConfigureAwait(false);
        var country = result.IsSuccessful ? result.CountryCode!.ToUpperInvariant() : string.Empty;
        _countries.Set(ip, country, TimeSpan.FromHours(6));
        return country;
    }
}

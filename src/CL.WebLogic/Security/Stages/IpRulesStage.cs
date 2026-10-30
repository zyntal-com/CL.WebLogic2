using CL.NetUtils;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Security.Pipeline;
using CL.WebLogic.Security.Rules;
using CL.WebLogic.Security.Services;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Security.Stages;

/// <summary>
/// Allow / deny by address and network, plus optional country rules through CL.NetUtils GeoIP
/// (cached). Rules come from <c>Security.IpRules</c> and any <see cref="IWebIpRuleProvider"/>.
/// Order: deny, then exempt (skips the rest and the reputation check), then the allow-list, then
/// countries. Addresses GeoIP cannot place (private, unknown) are not country-filtered.
/// </summary>
public sealed class IpRulesStage : IWebSecurityStage
{
    private readonly IWebLibraryResolver _libraries;
    private readonly IWebLogicContext _context;
    private readonly WebSecurityRules _rules;
    private readonly BoundedTtlCache<string> _countries;
    private int _warnedNoGeoIp;

    /// <summary>Creates the stage.</summary>
    public IpRulesStage(IWebLibraryResolver libraries, IWebLogicContext context, WebSecurityRules rules, TimeProvider? time = null)
    {
        _libraries = libraries ?? throw new ArgumentNullException(nameof(libraries));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
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
        var rules = await _rules.GetIpRulesAsync(context.Settings, cancellationToken).ConfigureAwait(false);
        var ip = context.ClientIp;
        if (rules.MatchDeny(ip) is { } denied)
            return WebSecurityDecision.Reject(StatusCodes.Status403Forbidden, "ip-denied", denied);
        if (rules.MatchExempt(ip) is not null)
        {
            context.IpExempt = true;
            return WebSecurityDecision.Continue;
        }
        if (rules.HasAllowList && rules.MatchAllow(ip) is null)
            return WebSecurityDecision.Reject(StatusCodes.Status403Forbidden, "ip-not-allowed");

        if (!rules.HasCountryRules || ip is null)
            return WebSecurityDecision.Continue;

        var country = await LookupCountryAsync(ip.ToString(), cancellationToken).ConfigureAwait(false);
        if (country.Length == 0)
        {
            // Unknown country: an allow-list fails open unless the operator asked otherwise.
            return context.Settings.Config.IpRules.RefuseUnknownCountry && !rules.CountryAllowed("")
                ? WebSecurityDecision.Reject(StatusCodes.Status403Forbidden, "country-unknown")
                : WebSecurityDecision.Continue;
        }
        if (rules.MatchDenyCountry(country) is { } countryRule)
            return WebSecurityDecision.Reject(StatusCodes.Status403Forbidden, "country-denied:" + country, countryRule);
        if (!rules.CountryAllowed(country))
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
                _context.Logger.Warning("Country rules are configured but CL.NetUtils GeoIP is not available; country rules are skipped "
                    + "(an allow-list lets everyone in unless ipRules.refuseUnknownCountry is on).");
            return string.Empty;
        }

        var result = await netUtils.GeoIp.LookupIpAsync(ip, cancellationToken).ConfigureAwait(false);
        var country = result.IsSuccessful ? result.CountryCode!.ToUpperInvariant() : string.Empty;
        _countries.Set(ip, country, TimeSpan.FromHours(6));
        return country;
    }
}

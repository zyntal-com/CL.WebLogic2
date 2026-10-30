using System.Text.RegularExpressions;
using CL.WebLogic.Auth.External;
using CL.WebLogic.Runtime.Models;
using CodeLogic.Core.Results;

namespace CL.WebLogic.ExternalLogins.Steam;

/// <summary>
/// "Sign in through Steam" — OpenID 2.0 against steamcommunity.com. No API key is needed: the
/// assertion is verified by posting it back to Steam (<c>check_authentication</c>). The toolkit's
/// <see cref="WebExternalLoginFlow"/> handles state, expiry and the return URL; this class only
/// speaks the provider's protocol. Register with <c>web.AddSteamLogin()</c>; the provider key is
/// the 64-bit SteamID. Steam gives no e-mail address.
/// </summary>
public sealed partial class SteamOpenIdProvider : IWebExternalLoginProvider
{
    private const string Endpoint = "https://steamcommunity.com/openid/login";
    private const string OpenIdNs = "http://specs.openid.net/auth/2.0";
    private const string IdentifierSelect = "http://specs.openid.net/auth/2.0/identifier_select";

    private readonly IHttpClientFactory _http;

    /// <summary>Creates the provider.</summary>
    public SteamOpenIdProvider(IHttpClientFactory http) => _http = http;

    /// <inheritdoc/>
    public string Name => "steam";

    /// <inheritdoc/>
    public string DisplayName => "Steam";

    /// <inheritdoc/>
    public IReadOnlyList<string> RedirectOrigins { get; } = ["https://steamcommunity.com"];

    /// <inheritdoc/>
    public ValueTask<string> GetChallengeUrlAsync(WebExternalChallenge challenge, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(challenge);
        var returnTo = ReturnTo(challenge);
        var realm = new Uri(challenge.CallbackUrl).GetLeftPart(UriPartial.Authority);
        var query = string.Join('&', new[]
        {
            ("openid.ns", OpenIdNs),
            ("openid.mode", "checkid_setup"),
            ("openid.return_to", returnTo),
            ("openid.realm", realm),
            ("openid.identity", IdentifierSelect),
            ("openid.claimed_id", IdentifierSelect)
        }.Select(p => p.Item1 + "=" + Uri.EscapeDataString(p.Item2)));
        return ValueTask.FromResult(Endpoint + "?" + query);
    }

    /// <inheritdoc/>
    public async ValueTask<Result<WebExternalLoginInfo>> HandleCallbackAsync(WebRequestContext request, WebExternalChallenge challenge, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(challenge);
        var query = request.HttpContext.Request.Query;
        string Get(string key) => query.TryGetValue(key, out var v) ? v.ToString() : string.Empty;

        if (Get("openid.mode") != "id_res")
            return Fail("The Steam sign-in was cancelled.");
        if (Get("openid.op_endpoint") != Endpoint || Get("openid.return_to") != ReturnTo(challenge))
            return Fail("The Steam response was not meant for this site.");
        var claimed = Get("openid.claimed_id");
        var match = ClaimedId().Match(claimed);
        if (!match.Success || Get("openid.identity") != claimed)
            return Fail("Steam returned an unexpected identity.");

        // Ask Steam whether it really issued this assertion.
        var fields = query.Where(p => p.Key.StartsWith("openid.", StringComparison.Ordinal))
            .Select(p => new KeyValuePair<string, string>(p.Key, p.Key == "openid.mode" ? "check_authentication" : p.Value.ToString()))
            .ToList();
        try
        {
            using var content = new FormUrlEncodedContent(fields);
            using var response = await _http.CreateClient(WebExternalLoginHttp.ClientName).PostAsync(new Uri(Endpoint), content, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode || !body.Split('\n').Any(line => line.Trim() == "is_valid:true"))
                return Fail("Steam did not confirm the sign-in.");
        }
        catch (HttpRequestException)
        {
            return Fail("Steam could not be reached; try again.");
        }

        return Result<WebExternalLoginInfo>.Success(new WebExternalLoginInfo { Provider = Name, ProviderKey = match.Groups[1].Value });
    }

    private static string ReturnTo(WebExternalChallenge challenge) =>
        challenge.CallbackUrl + (challenge.CallbackUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?") + "state=" + Uri.EscapeDataString(challenge.State);

    private static Result<WebExternalLoginInfo> Fail(string message) =>
        Result<WebExternalLoginInfo>.Failure(Error.Unauthorized("weblogic.external.failed", message));

    [GeneratedRegex(@"^https://steamcommunity\.com/openid/id/(7656119[0-9]{10})$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ClaimedId();
}

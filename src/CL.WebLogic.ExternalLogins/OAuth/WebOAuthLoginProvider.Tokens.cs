using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CL.WebLogic.Auth.External;
using CodeLogic.Core.Results;

namespace CL.WebLogic.ExternalLogins.OAuth;

public sealed partial class WebOAuthLoginProvider
{
    private const long MaxResponseBytes = 256 * 1024;

    private async Task<(string Authorization, string Token, string? UserInfo, WebOidcConfiguration? Oidc)> EndpointsAsync(CancellationToken cancellationToken)
    {
        if (_options.Authority is null)
            return (_options.AuthorizationEndpoint!, _options.TokenEndpoint!, _options.UserInfoEndpoint, null);
        var discovered = await _metadata.GetConfigurationAsync(_options.Authority, cancellationToken).ConfigureAwait(false);
        return (_options.AuthorizationEndpoint ?? discovered.AuthorizationEndpoint, _options.TokenEndpoint ?? discovered.TokenEndpoint,
            _options.UserInfoEndpoint ?? discovered.UserInfoEndpoint, discovered);
    }

    private async Task<(string? AccessToken, string? IdToken)> ExchangeCodeAsync(string tokenEndpoint, string code, WebExternalChallenge challenge, CancellationToken cancellationToken)
    {
        WebOidcMetadata.RequireHttps(tokenEndpoint);
        var form = new List<KeyValuePair<string, string>>
        {
            new("grant_type", "authorization_code"),
            new("code", code),
            new("redirect_uri", challenge.CallbackUrl)
        };
        if (_options.UsePkce)
            form.Add(new("code_verifier", challenge.CodeVerifier));

        using var message = new HttpRequestMessage(HttpMethod.Post, new Uri(tokenEndpoint));
        if (_options.ClientAuthentication == WebOAuthClientAuthentication.Basic && _options.ClientSecret is not null)
        {
            var credentials = Uri.EscapeDataString(_options.ClientId) + ":" + Uri.EscapeDataString(_options.ClientSecret);
            message.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials)));
        }
        else
        {
            form.Add(new("client_id", _options.ClientId));
            if (_options.ClientSecret is not null)
                form.Add(new("client_secret", _options.ClientSecret));
        }
        message.Content = new FormUrlEncodedContent(form);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var json = await SendAsync(message, cancellationToken).ConfigureAwait(false);
        var root = json.RootElement;
        if (Text(root, "error") is { } error)
            throw new InvalidOperationException("token endpoint error=" + error);
        return (Text(root, "access_token"), Text(root, "id_token"));
    }

    private async Task<JsonElement> GetJsonAsync(string url, string accessToken, CancellationToken cancellationToken)
    {
        WebOidcMetadata.RequireHttps(url);
        using var message = new HttpRequestMessage(HttpMethod.Get, new Uri(url));
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var json = await SendAsync(message, cancellationToken).ConfigureAwait(false);
        return json.RootElement.Clone();
    }

    private async Task<JsonDocument> SendAsync(HttpRequestMessage message, CancellationToken cancellationToken)
    {
        message.Headers.UserAgent.ParseAdd("CL.WebLogic"); // GitHub rejects requests without one
        using var response = await _http.CreateClient(WebExternalLoginHttp.ClientName).SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (response.Content.Headers.ContentLength > MaxResponseBytes)
            throw new InvalidOperationException("response too large");
        var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false), cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode && Text(document.RootElement, "error") is null)
        {
            document.Dispose();
            throw new HttpRequestException($"{message.RequestUri?.Host} answered {(int)response.StatusCode}");
        }
        return document;
    }

    private async Task<Result<WebExternalLoginInfo>> ToInfoAsync(JsonElement profile, string? accessToken, CancellationToken cancellationToken)
    {
        if (profile.ValueKind != JsonValueKind.Object || Scalar(profile, _options.SubjectClaim) is not { Length: > 0 and <= 255 } subject)
            return Fail("The provider did not say who signed in.", "no '" + _options.SubjectClaim + "' claim");

        var email = Text(profile, _options.EmailClaim);
        var verified = email is not null && _options.EmailVerifiedClaim is not null && IsTrue(profile, _options.EmailVerifiedClaim);
        if (_options.EmailsEndpoint is not null && accessToken is not null && (email is null || !verified))
        {
            var emails = await GetJsonAsync(_options.EmailsEndpoint, accessToken, cancellationToken).ConfigureAwait(false);
            if (emails.ValueKind == JsonValueKind.Array
                && emails.EnumerateArray().FirstOrDefault(e => IsTrue(e, "primary") && IsTrue(e, "verified")) is { ValueKind: JsonValueKind.Object } primary)
                (email, verified) = (Text(primary, "email"), true);
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var claim in new[] { "picture", "avatar_url", "avatar", "locale", "preferred_username", "login" })
        {
            if (Scalar(profile, claim) is { Length: > 0 and <= 2048 } value)
                values[claim] = value;
        }
        return Result<WebExternalLoginInfo>.Success(new WebExternalLoginInfo
        {
            Provider = Name,
            ProviderKey = subject,
            UserName = Scalar(profile, _options.NameClaim),
            Email = email,
            EmailVerified = verified,
            Values = values
        });
    }

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>A string or number claim as text (GitHub and Discord ids are numbers or numeric strings).</summary>
    private static string? Scalar(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value)
            ? value.ValueKind switch { JsonValueKind.String => value.GetString(), JsonValueKind.Number => value.GetRawText(), _ => null }
            : null;

    private static bool IsTrue(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value)
        && (value.ValueKind == JsonValueKind.True || (value.ValueKind == JsonValueKind.String && value.GetString() == "true"));
}

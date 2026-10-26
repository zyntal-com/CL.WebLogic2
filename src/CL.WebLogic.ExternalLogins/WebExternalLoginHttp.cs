namespace CL.WebLogic.ExternalLogins;

/// <summary>The named <see cref="HttpClient"/> the providers use (10-second timeout).</summary>
public static class WebExternalLoginHttp
{
    /// <summary>Name of the client; configure it (proxy, handler) with <c>services.AddHttpClient(ClientName)</c>.</summary>
    public const string ClientName = "weblogic-external-logins";
}

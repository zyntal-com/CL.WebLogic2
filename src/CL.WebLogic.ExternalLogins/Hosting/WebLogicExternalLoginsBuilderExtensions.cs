using CL.WebLogic.Auth.External;
using CL.WebLogic.ExternalLogins.OAuth;
using CL.WebLogic.ExternalLogins.Steam;
using CL.WebLogic.Hosting.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CL.WebLogic.ExternalLogins.Hosting;

/// <summary>
/// Registers external sign-in providers. Map the challenge and callback routes with
/// <see cref="WebExternalLoginFlow"/> and finish with <c>request.Auth.SignInExternalAsync</c>
/// (which applies the account rules and the second factor).
/// </summary>
public static class WebLogicExternalLoginsBuilderExtensions
{
    /// <summary>"Sign in through Steam" (OpenID 2.0, no API key needed).</summary>
    public static WebLogicBuilder AddSteamLogin(this WebLogicBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        AddShared(builder.Services);
        Reserve(builder.Services, "steam");
        builder.Services.AddSingleton<IWebExternalLoginProvider, SteamOpenIdProvider>();
        return builder;
    }

    /// <summary>An OAuth 2.0 / OpenID Connect provider (see <see cref="WebOAuthPresets"/>).</summary>
    public static WebLogicBuilder AddOAuthLogin(this WebLogicBuilder builder, WebOAuthOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        AddShared(builder.Services);
        Reserve(builder.Services, options.Name);
        builder.Services.AddSingleton<IWebExternalLoginProvider>(services => ActivatorUtilities.CreateInstance<WebOAuthLoginProvider>(services, options));
        return builder;
    }

    /// <summary>Sign in with Google.</summary>
    public static WebLogicBuilder AddGoogleLogin(this WebLogicBuilder builder, string clientId, string clientSecret) =>
        builder.AddOAuthLogin(WebOAuthPresets.Google(clientId, clientSecret));

    /// <summary>Sign in with a Microsoft account (<paramref name="tenant"/>: common, organizations, consumers or a tenant id).</summary>
    public static WebLogicBuilder AddMicrosoftLogin(this WebLogicBuilder builder, string clientId, string clientSecret, string tenant = "common") =>
        builder.AddOAuthLogin(WebOAuthPresets.Microsoft(clientId, clientSecret, tenant));

    /// <summary>Sign in with GitHub.</summary>
    public static WebLogicBuilder AddGitHubLogin(this WebLogicBuilder builder, string clientId, string clientSecret) =>
        builder.AddOAuthLogin(WebOAuthPresets.GitHub(clientId, clientSecret));

    /// <summary>Sign in with Discord.</summary>
    public static WebLogicBuilder AddDiscordLogin(this WebLogicBuilder builder, string clientId, string clientSecret) =>
        builder.AddOAuthLogin(WebOAuthPresets.Discord(clientId, clientSecret));

    private static void AddShared(IServiceCollection services)
    {
        if (!services.Any(d => d.ServiceType == typeof(WebOidcMetadata)))
            services.AddHttpClient(WebExternalLoginHttp.ClientName, client => client.Timeout = TimeSpan.FromSeconds(10));
        services.TryAddSingleton<WebOidcMetadata>();
    }

    private static void Reserve(IServiceCollection services, string name)
    {
        if (services.Any(d => d.ServiceType == typeof(ProviderName) && ((ProviderName)d.ImplementationInstance!).Name == name))
            throw new InvalidOperationException($"An external login provider named '{name}' is already registered.");
        services.AddSingleton(new ProviderName(name));
    }

    private sealed record ProviderName(string Name);
}

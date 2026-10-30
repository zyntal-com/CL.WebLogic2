using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Results;
using CL.WebLogic.Routing;
using CL.WebLogic.Routing.Models;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Runtime.Services;

namespace CL.WebLogic.Localization.Services;

/// <summary>
/// The culture switch endpoint, mapped only when localization is enabled at startup:
/// <c>GET {Localization.SwitchPath}?culture=sv&amp;returnUrl=/page</c> sets the culture cookie and
/// redirects to the (local) return URL. An unsupported culture answers 400 and sets nothing.
/// </summary>
public sealed class LocalizationContributor : IWebContributor
{
    /// <summary>Descriptor of the built-in localization routes.</summary>
    public static WebContributorDescriptor Descriptor { get; } = new()
    {
        Id = "weblogic.localization",
        Name = "WebLogic localization",
        Kind = "System",
        Description = "Sets the culture cookie."
    };

    private readonly WebLogicConfigHolder _config;
    private readonly WebCultureCookie _cookie;

    /// <summary>Creates the contributor.</summary>
    public LocalizationContributor(WebLogicConfigHolder config, WebCultureCookie cookie)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _cookie = cookie ?? throw new ArgumentNullException(nameof(cookie));
    }

    /// <inheritdoc/>
    public Task ContributeAsync(WebContributionBuilder builder, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var config = _config.Current.Localization;
        if (!config.Enabled)
            return Task.CompletedTask;

        builder.Map(WebRouteKind.System, config.SwitchPath, (request, _) =>
        {
            var culture = request.GetQuery(_config.Current.Localization.QueryParameter);
            if (!_cookie.TrySet(request.HttpContext, culture))
                return ValueTask.FromResult<WebResult>(WebResult.Status(400));
            return ValueTask.FromResult<WebResult>(WebResult.Redirect(WebUrl.SafeReturnUrl(request.GetQuery("returnUrl"))));
        }, "GET")
        .AllowAnonymous()
        .WithName("weblogic.culture")
        .WithDescription("Sets the culture cookie and redirects to a local return URL.");
        return Task.CompletedTask;
    }
}

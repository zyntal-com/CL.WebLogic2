using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Results;
using CL.WebLogic.Routing;
using CL.WebLogic.Routing.Models;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Security.Headers;
using Microsoft.Extensions.Hosting;

namespace CL.WebLogic.Client.Services;

/// <summary>Serves the client files and, in Development, the client test page.</summary>
public sealed class ClientContributor : IWebContributor
{
    /// <summary>Contributor id.</summary>
    public const string ContributorId = "weblogic.client";

    private readonly ClientAssetCatalog _catalog;
    private readonly ClientScriptProvider _scripts;
    private readonly ICspNonceProvider _nonces;
    private readonly WebLogicConfigHolder _config;
    private readonly IHostEnvironment _environment;

    /// <summary>Creates the contributor.</summary>
    public ClientContributor(ClientAssetCatalog catalog, ClientScriptProvider scripts, ICspNonceProvider nonces, WebLogicConfigHolder config, IHostEnvironment environment)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _scripts = scripts ?? throw new ArgumentNullException(nameof(scripts));
        _nonces = nonces ?? throw new ArgumentNullException(nameof(nonces));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    /// <summary>Descriptor.</summary>
    public static WebContributorDescriptor Descriptor { get; } = new()
    {
        Id = ContributorId,
        Name = "WebLogic client",
        Kind = "System",
        Description = "Serves the WebLogic client runtime (and its test page in Development)."
    };

    /// <inheritdoc/>
    public Task ContributeAsync(WebContributionBuilder builder, CancellationToken cancellationToken)
    {
        builder.Map(WebRouteKind.Asset, ClientAssetCatalog.PathPrefix + "{file}", (request, _) =>
            ValueTask.FromResult<WebResult>(_catalog.TryResolve(request.GetRouteValue("file"), out var asset, out var fingerprinted)
                ? new ClientAssetResult(asset, fingerprinted)
                : WebResult.Status(404)), "GET", "HEAD")
            .AllowAnonymous()
            .WithName("weblogic.client")
            .WithDescription("WebLogic client runtime files");

        if (_environment.IsDevelopment() && _config.Current.Client.TestPageInDevelopment)
        {
            builder.MapPage("/_weblogic/tests", (request, _) => ValueTask.FromResult<WebResult>(TestPage(request)))
                .AllowAnonymous()
                .WithName("weblogic.client-tests")
                .WithDescription("Browser tests of the WebLogic client (Development only)");
        }
        return Task.CompletedTask;
    }

    private HtmlResult TestPage(Runtime.Models.WebRequestContext request)
    {
        var nonce = _nonces.GetNonce(request.HttpContext);
        var basePath = request.HttpContext.Request.PathBase.Value ?? string.Empty;
        var tests = _catalog.Get("weblogic.tests.esm.js");
        var page = "<!DOCTYPE html>\n<html lang=\"en\">\n<head>\n<meta charset=\"utf-8\">\n<title>WebLogic client tests</title>\n"
            + _scripts.GetScriptTags(request.HttpContext, nonce) + "\n</head>\n<body>\n<h1>WebLogic client tests</h1>\n<ul id=\"wl-test-results\"></ul>\n"
            + (tests is null ? "" : "<script type=\"module\" src=\"" + basePath + ClientAssetCatalog.PathPrefix + tests.HashedName + "\" integrity=\"" + tests.Integrity
                + "\" crossorigin=\"anonymous\" nonce=\"" + nonce + "\"></script>\n")
            + "</body>\n</html>\n";
        return WebResult.Html(page);
    }
}

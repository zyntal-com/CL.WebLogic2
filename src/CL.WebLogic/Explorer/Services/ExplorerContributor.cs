using System.Reflection;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Results;
using CL.WebLogic.Routing;
using CL.WebLogic.Routing.Models;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Security.Headers;
using CL.WebLogic.Templates.Escaping;
using Microsoft.Extensions.Hosting;

namespace CL.WebLogic.Explorer.Services;

/// <summary>
/// The API explorer: <c>/_weblogic/explorer</c> (page) and <c>/_weblogic/explorer.json</c>.
/// In Development it is on by default and anonymous; elsewhere it needs <c>Explorer.InProduction</c>
/// and the <c>Explorer.Permission</c> permission. The page is static HTML from embedded resources.
/// </summary>
public sealed class ExplorerContributor : IWebContributor
{
    /// <summary>Contributor id.</summary>
    public const string ContributorId = "weblogic.explorer";

    private static readonly Lazy<(string Html, string Script, string Style)> Assets = new(Load);

    private readonly ExplorerModelBuilder _model;
    private readonly WebLogicConfigHolder _config;
    private readonly IHostEnvironment _environment;
    private readonly ICspNonceProvider _nonces;

    /// <summary>Creates the contributor.</summary>
    public ExplorerContributor(ExplorerModelBuilder model, WebLogicConfigHolder config, IHostEnvironment environment, ICspNonceProvider nonces)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _nonces = nonces ?? throw new ArgumentNullException(nameof(nonces));
    }

    /// <summary>Descriptor.</summary>
    public static WebContributorDescriptor Descriptor { get; } = new()
    {
        Id = ContributorId,
        Name = "WebLogic explorer",
        Kind = "System",
        Description = "Lists routes, protection, the security pipeline, widgets, forms and realtime."
    };

    /// <inheritdoc/>
    public Task ContributeAsync(WebContributionBuilder builder, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var config = _config.Current.Explorer;
        var development = _environment.IsDevelopment();
        if (development ? !config.InDevelopment : !config.InProduction)
            return Task.CompletedTask;

        var group = builder.MapGroup("/_weblogic");
        if (development)
            group.AllowAnonymous();
        else
            group.RequirePermissions(config.Permission);

        group.MapPage("/explorer", (request, _) =>
        {
            var nonce = _nonces.GetNonce(request.HttpContext);
            var (html, script, style) = Assets.Value;
            var page = html
                .Replace("{style}", "<style nonce=\"" + nonce + "\">" + style + "</style>", StringComparison.Ordinal)
                .Replace("{script}", "<script nonce=\"" + nonce + "\">" + script + "</script>", StringComparison.Ordinal)
                .Replace("{json}", TemplateEncoder.Html.Encode((request.HttpContext.Request.PathBase.Value ?? "") + "/_weblogic/explorer.json"), StringComparison.Ordinal)
                .Replace("{site}", TemplateEncoder.Html.Encode(_config.Current.SiteName), StringComparison.Ordinal);
            return ValueTask.FromResult<WebResult>(WebResult.Html(page));
        }).WithName("weblogic.explorer").WithDescription("API explorer");

        group.MapApi("/explorer.json", (_, _) => ValueTask.FromResult<WebResult>(WebResult.Json(_model.Build())))
            .WithName("weblogic.explorer.json")
            .WithDescription("API explorer data");
        return Task.CompletedTask;
    }

    private static (string, string, string) Load() =>
        (Read("explorer.html"), Read("explorer.js"), Read("explorer.css"));

    private static string Read(string name)
    {
        using var stream = typeof(ExplorerContributor).Assembly.GetManifestResourceStream("CL.WebLogic.Explorer." + name)
            ?? throw new InvalidOperationException($"Explorer resource '{name}' is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

using System.Text;
using System.Text.Json;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Runtime.Models;
using CL.WebLogic.Runtime.Services;
using CL.WebLogic.Templates.Escaping;
using CL.WebLogic.Templates.Rendering;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace CL.WebLogic.Client.Services;

/// <summary>
/// Writes <c>{weblogic_scripts}</c>: the configuration JSON block, the stylesheet, the client
/// bundle and the UI adapter — fingerprinted URLs, SRI integrity and the request's CSP nonce.
/// No CSRF token is embedded (pages stay cacheable); the client fetches one on first use. A waiting
/// <see cref="WebFlash"/> message is taken and written as <c>flash</c> (the client shows it as a toast).
/// </summary>
public sealed class ClientScriptProvider : ITemplateScriptProvider
{
    private readonly ClientAssetCatalog _catalog;
    private readonly WebLogicConfigHolder _config;
    private readonly IHostEnvironment _environment;
    private readonly WebFlash? _flash;

    /// <summary>Creates the provider.</summary>
    public ClientScriptProvider(ClientAssetCatalog catalog, WebLogicConfigHolder config, IHostEnvironment environment, WebFlash? flash = null)
    {
        _flash = flash;
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    /// <inheritdoc/>
    public string GetScriptTags(HttpContext? httpContext, string nonce) => GetScriptTags(httpContext, nonce, null);

    /// <inheritdoc/>
    public string GetScriptTags(HttpContext? httpContext, string nonce, string? layout)
    {
        var config = _config.Current;
        var client = config.Client;
        if (!client.Enabled)
            return string.Empty;

        var basePath = httpContext?.Request.PathBase.Value ?? string.Empty;
        var module = !string.Equals(client.Format, "classic", StringComparison.OrdinalIgnoreCase);
        var html = new StringBuilder();

        html.Append("<script type=\"application/json\" id=\"wl-config\">")
            .Append(ConfigJson(basePath, client.Navigation, layout, httpContext is null ? null : _flash?.Take(httpContext)))
            .Append("</script>\n");

        if (client.IncludeStylesheet)
            AppendStylesheet(html, basePath, "weblogic.css");
        AppendScript(html, basePath, module ? "weblogic.esm.js" : "weblogic.js", module, nonce);

        if (string.Equals(client.UiAdapter, "bootstrap5", StringComparison.OrdinalIgnoreCase))
            AppendScript(html, basePath, module ? "weblogic.bootstrap5.esm.js" : "weblogic.bootstrap5.js", module, nonce);
        return html.ToString().TrimEnd('\n');
    }

    /// <summary>The client configuration JSON (safe inside a script element).</summary>
    public string ConfigJson(string basePath, bool navigation) => ConfigJson(basePath, navigation, null);

    /// <summary>
    /// The client configuration JSON (safe inside a script element). <paramref name="layout"/> (the document's outermost
    /// template) lets partial navigation fall back to a full load when the next page uses another layout.
    /// </summary>
    public string ConfigJson(string basePath, bool navigation, string? layout) => ConfigJson(basePath, navigation, layout, null);

    /// <summary>The client configuration JSON with a one-shot <paramref name="flash"/> message (shown as a toast).</summary>
    public string ConfigJson(string basePath, bool navigation, string? layout, WebFlashMessage? flash)
    {
        var csrf = _config.Current.Security.Csrf;
        var realtime = _config.Current.Realtime;
        var payload = new Dictionary<string, object?>
        {
            ["version"] = _catalog.Version,
            ["basePath"] = basePath,
            ["csrfHeader"] = csrf.HeaderName,
            ["csrfField"] = csrf.FieldName,
            ["csrfUrl"] = "/_weblogic/csrf",
            ["navigation"] = navigation,
            ["layout"] = layout,
            ["debug"] = _environment.IsDevelopment(),
            ["flash"] = flash is null ? null : new Dictionary<string, object?> { ["message"] = flash.Message, ["variant"] = flash.Variant },
            ["realtime"] = new Dictionary<string, object?>
            {
                ["enabled"] = realtime.Enabled,
                ["transport"] = realtime.WebSockets ? realtime.ClientTransport : "sse",
                ["sseUrl"] = "/_weblogic/realtime/sse",
                ["wsUrl"] = "/_weblogic/realtime/ws",
                ["autoConnect"] = realtime.Enabled && realtime.ClientAutoConnect
            }
        };
        // The default encoder escapes <, > and &, so the JSON cannot end the script element.
        return JsonSerializer.Serialize(payload, WebJson.Default);
    }

    private void AppendScript(StringBuilder html, string basePath, string name, bool module, string nonce)
    {
        var asset = _catalog.Get(name);
        if (asset is null)
            return;
        html.Append("<script");
        if (module)
            html.Append(" type=\"module\"");
        else
            html.Append(" defer");
        html.Append(" src=\"").Append(Attr(basePath + ClientAssetCatalog.PathPrefix + asset.HashedName)).Append('"')
            .Append(" integrity=\"").Append(Attr(asset.Integrity)).Append("\" crossorigin=\"anonymous\"");
        if (!string.IsNullOrEmpty(nonce))
            html.Append(" nonce=\"").Append(Attr(nonce)).Append('"');
        html.Append("></script>\n");
    }

    private void AppendStylesheet(StringBuilder html, string basePath, string name)
    {
        var asset = _catalog.Get(name);
        if (asset is null)
            return;
        html.Append("<link rel=\"stylesheet\" href=\"").Append(Attr(basePath + ClientAssetCatalog.PathPrefix + asset.HashedName))
            .Append("\" integrity=\"").Append(Attr(asset.Integrity)).Append("\" crossorigin=\"anonymous\">\n");
    }

    private static string Attr(string value) => TemplateEncoder.Html.Encode(value);
}

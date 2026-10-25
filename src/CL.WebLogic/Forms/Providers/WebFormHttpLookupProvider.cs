using System.Net.Http.Json;
using System.Text.Json;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Forms.Models;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Security.Services;

namespace CL.WebLogic.Forms.Providers;

/// <summary>
/// Options from a JSON HTTP API. The URL is an absolute <c>https</c> template configured in code
/// (<c>{term}</c> is replaced, URL-encoded) — never built from the request — and its host must
/// be in <c>Forms.AllowedLookupHosts</c>. No redirects, 5 s timeout, 1 MB limit, bounded cache.
/// The response must be a JSON array of objects with the configured value and label properties.
/// </summary>
public sealed class WebFormHttpLookupProvider : WebFormOptionsProvider
{
    private static readonly HttpClient Client = new(new SocketsHttpHandler { AllowAutoRedirect = false, PooledConnectionLifetime = TimeSpan.FromMinutes(5) })
    {
        Timeout = TimeSpan.FromSeconds(5),
        MaxResponseContentBufferSize = 1024 * 1024
    };

    private readonly string _name;
    private readonly string _urlTemplate;
    private readonly string _valueProperty;
    private readonly string _labelProperty;
    private readonly TimeSpan _cacheFor;
    private readonly bool _allowAnonymous;
    private readonly WebLogicConfigHolder _config;
    private readonly IWebLogicContext _context;
    private readonly BoundedTtlCache<IReadOnlyList<WebFieldOption>> _cache = new(1000);

    /// <summary>Creates the provider.</summary>
    public WebFormHttpLookupProvider(string name, string urlTemplate, WebLogicConfigHolder config, IWebLogicContext context,
        string valueProperty = "value", string labelProperty = "label", int cacheSeconds = 300, bool allowAnonymous = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (!Uri.TryCreate(urlTemplate.Replace("{term}", "x", StringComparison.Ordinal), UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("The lookup URL must be an absolute https URL.", nameof(urlTemplate));
        _name = name;
        _urlTemplate = urlTemplate;
        _valueProperty = valueProperty;
        _labelProperty = labelProperty;
        _cacheFor = TimeSpan.FromSeconds(Math.Max(0, cacheSeconds));
        _allowAnonymous = allowAnonymous;
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc/>
    public override string Name => _name;

    /// <inheritdoc/>
    public override bool AllowAnonymous => _allowAnonymous;

    /// <inheritdoc/>
    public override ValueTask<IReadOnlyList<WebFieldOption>> GetOptionsAsync(WebFormOptionsRequest request, CancellationToken cancellationToken) =>
        new(FetchAsync("", cancellationToken));

    /// <inheritdoc/>
    public override ValueTask<IReadOnlyList<WebFieldOption>> SearchAsync(string term, WebFormOptionsRequest request, CancellationToken cancellationToken) =>
        new(FetchAsync(term ?? "", cancellationToken));

    private async Task<IReadOnlyList<WebFieldOption>> FetchAsync(string term, CancellationToken cancellationToken)
    {
        if (term.Length > 100)
            return [];
        if (_cache.TryGet(term, out var cached))
            return cached;

        var url = new Uri(_urlTemplate.Replace("{term}", Uri.EscapeDataString(term), StringComparison.Ordinal));
        if (!_config.Current.Forms.AllowedLookupHosts.Contains(url.Host, StringComparer.OrdinalIgnoreCase))
        {
            _context.Logger.Warning($"Lookup '{_name}': host '{url.Host}' is not in Forms.AllowedLookupHosts.");
            return [];
        }

        try
        {
            using var response = await Client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return [];
            var items = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken).ConfigureAwait(false);
            if (items.ValueKind != JsonValueKind.Array)
                return [];
            var options = new List<WebFieldOption>();
            foreach (var item in items.EnumerateArray().Take(200))
            {
                if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty(_valueProperty, out var value))
                {
                    var text = value.ToString();
                    var label = item.TryGetProperty(_labelProperty, out var l) ? l.ToString() : text;
                    options.Add(new WebFieldOption(text, label));
                }
            }
            if (_cacheFor > TimeSpan.Zero)
                _cache.Set(term, options, _cacheFor);
            return options;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException)
        {
            _context.Logger.Warning($"Lookup '{_name}' failed: {ex.Message}");
            return [];
        }
    }
}

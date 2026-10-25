using CL.WebLogic.Content.Services;
using CL.WebLogic.Localization.Services;
using CL.WebLogic.Runtime.Models;
using CL.WebLogic.Security.Headers;
using CL.WebLogic.Templates.Rendering;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Testing.Services;

/// <summary>
/// Renders templates in unit tests without a host: add sources to <see cref="Templates"/>, swap
/// any hook (CSRF, widgets, localizer…) and call <see cref="RenderAsync"/>.
/// </summary>
public sealed class TemplateTestRenderer
{
    /// <summary>Creates the renderer; templates are compiled with <paramref name="compiler"/> (closure by default).</summary>
    public TemplateTestRenderer(ITemplateCompiler? compiler = null) => Templates = new InMemoryTemplateLoader(compiler);

    /// <summary>Template sources.</summary>
    public InMemoryTemplateLoader Templates { get; }

    /// <summary>Globals available to every template.</summary>
    public Dictionary<string, object?> Globals { get; } = new(StringComparer.OrdinalIgnoreCase) { ["site_name"] = "Test Site" };

    /// <summary>Custom filters.</summary>
    public List<ITemplateFilter> Filters { get; } = [];

    /// <summary>CSRF provider (none by default).</summary>
    public ITemplateCsrfProvider Csrf { get; set; } = new NullTemplateCsrfProvider();

    /// <summary>Widget renderer (writes a comment by default).</summary>
    public ITemplateWidgetRenderer Widgets { get; set; } = new NullTemplateWidgetRenderer();

    /// <summary>Form renderer for <c>{form:}</c> (writes a comment by default).</summary>
    public ITemplateFormRenderer Forms { get; set; } = NullTemplateFormRenderer.Instance;

    /// <summary>Script provider (none by default).</summary>
    public ITemplateScriptProvider Scripts { get; set; } = new NullTemplateScriptProvider();

    /// <summary>Asset URLs (<c>/assets/{path}</c> by default).</summary>
    public ITemplateAssetUrlResolver Assets { get; set; } = new PrefixAssetUrlResolver("/assets");

    /// <summary>Group / permission checks.</summary>
    public ITemplateAuthorizer Authorizer { get; set; } = new ClaimsTemplateAuthorizer();

    /// <summary>Localizer (returns keys by default).</summary>
    public IWebTextLocalizer Localizer { get; set; } = new PassthroughTextLocalizer();

    /// <summary>Nonce provider.</summary>
    public ICspNonceProvider Nonce { get; set; } = new CspNonceProvider();

    /// <summary>Markdown renderer.</summary>
    public IWebMarkdownRenderer Markdown { get; set; } = new WebMarkdownRenderer();

    /// <summary>HTML sanitizer.</summary>
    public IWebHtmlSanitizer Sanitizer { get; set; } = new WebHtmlSanitizer();

    /// <summary>Renders <paramref name="templatePath"/>.</summary>
    public Task<string> RenderAsync(string templatePath, object? model = null, HttpContext? httpContext = null, WebPageMeta? meta = null, bool ignoreLayout = false)
    {
        var services = new TemplateRenderServices(
            Templates, new TemplateFilterRegistry(Filters), Csrf, Widgets, Scripts, Assets, Authorizer, Localizer, Nonce, Markdown, Sanitizer, Forms);
        var renderer = new WebTemplateRenderer(services, [new DictionaryGlobals(Globals)]);
        return renderer.RenderAsync(new TemplateRenderRequest
        {
            TemplatePath = templatePath,
            Model = model,
            HttpContext = httpContext,
            Meta = meta,
            IgnoreLayout = ignoreLayout
        }, CancellationToken.None);
    }

    /// <summary>Adds <paramref name="source"/> as <c>templates/test.html</c> and renders it.</summary>
    public Task<string> RenderSourceAsync(string source, object? model = null, HttpContext? httpContext = null)
    {
        Templates.Add("templates/test", source);
        return RenderAsync("templates/test", model, httpContext);
    }

    private sealed class DictionaryGlobals(Dictionary<string, object?> values) : ITemplateGlobalsProvider
    {
        public void Contribute(IDictionary<string, object?> globals, HttpContext? httpContext)
        {
            foreach (var (key, value) in values)
                globals[key] = value;
        }
    }
}

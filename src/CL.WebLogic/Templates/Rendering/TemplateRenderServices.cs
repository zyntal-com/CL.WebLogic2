using CL.WebLogic.Content.Services;
using CL.WebLogic.Localization.Services;
using CL.WebLogic.Security.Headers;

namespace CL.WebLogic.Templates.Rendering;

/// <summary>The services a render needs, bundled so compiled templates take a single dependency.</summary>
public sealed class TemplateRenderServices
{
    /// <summary>Creates the bundle.</summary>
    public TemplateRenderServices(
        ITemplateLoader loader,
        TemplateFilterRegistry filters,
        ITemplateCsrfProvider csrf,
        ITemplateWidgetRenderer widgets,
        ITemplateScriptProvider scripts,
        ITemplateAssetUrlResolver assets,
        ITemplateAuthorizer authorizer,
        IWebTextLocalizer localizer,
        ICspNonceProvider nonce,
        IWebMarkdownRenderer markdown,
        IWebHtmlSanitizer sanitizer,
        ITemplateFormRenderer? forms = null)
    {
        Loader = loader;
        Filters = filters;
        Csrf = csrf;
        Widgets = widgets;
        Scripts = scripts;
        Assets = assets;
        Authorizer = authorizer;
        Localizer = localizer;
        Nonce = nonce;
        Markdown = markdown;
        Sanitizer = sanitizer;
        Forms = forms ?? NullTemplateFormRenderer.Instance;
    }

    /// <summary>Loads partials and layouts.</summary>
    public ITemplateLoader Loader { get; }

    /// <summary>Filters.</summary>
    public TemplateFilterRegistry Filters { get; }

    /// <summary>CSRF tokens.</summary>
    public ITemplateCsrfProvider Csrf { get; }

    /// <summary><c>{form:…}</c>.</summary>
    public ITemplateFormRenderer Forms { get; }

    /// <summary>Widgets and widget areas.</summary>
    public ITemplateWidgetRenderer Widgets { get; }

    /// <summary><c>{weblogic_scripts}</c>.</summary>
    public ITemplateScriptProvider Scripts { get; }

    /// <summary><c>{asset:…}</c> URLs.</summary>
    public ITemplateAssetUrlResolver Assets { get; }

    /// <summary>Group and permission conditions.</summary>
    public ITemplateAuthorizer Authorizer { get; }

    /// <summary><c>{t:…}</c> texts.</summary>
    public IWebTextLocalizer Localizer { get; }

    /// <summary>CSP nonces.</summary>
    public ICspNonceProvider Nonce { get; }

    /// <summary><c>|markdown</c>.</summary>
    public IWebMarkdownRenderer Markdown { get; }

    /// <summary><c>|sanitize</c>.</summary>
    public IWebHtmlSanitizer Sanitizer { get; }
}

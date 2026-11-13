using System.Text;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Forms.AntiBot;
using CL.WebLogic.Forms.Validation;
using CL.WebLogic.Security.Antiforgery;
using CL.WebLogic.Templates.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.ObjectPool;

namespace CL.WebLogic.Forms.Rendering;

/// <summary>Services renderers need (bundled so custom renderers get them from DI in one piece).</summary>
public sealed class WebFormRendererServices(
    IWebCsrfTokens csrf,
    WebFormAntiBot antiBot,
    WebFormMessages messages,
    WebLogicConfigHolder config,
    IServiceProvider services)
{
    /// <summary>Pooled builders for the form and its controls (reused, so they keep their grown capacity).</summary>
    internal ObjectPool<StringBuilder> Builders { get; } = new DefaultObjectPoolProvider().CreateStringBuilderPool(initialCapacity: 4096, maximumRetainedCapacity: 256 * 1024);

    /// <summary>CSRF tokens.</summary>
    public IWebCsrfTokens Csrf { get; } = csrf;

    /// <summary>Anti-bot fields.</summary>
    public WebFormAntiBot AntiBot { get; } = antiBot;

    /// <summary>Messages.</summary>
    public WebFormMessages Messages { get; } = messages;

    /// <summary>Configuration.</summary>
    public WebLogicConfigHolder Config { get; } = config;

    /// <summary>The theme template renderer (resolved lazily: templates can render forms and forms can use templates).</summary>
    public IWebTemplateRenderer? Templates => services.GetService<IWebTemplateRenderer>();
}

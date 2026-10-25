using CL.WebLogic.Runtime.Models;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Templates.Rendering;

/// <summary>What to render.</summary>
public sealed class TemplateRenderRequest
{
    /// <summary>Template path, e.g. <c>templates/home</c> (<c>.html</c> is optional).</summary>
    public required string TemplatePath { get; init; }

    /// <summary>The model (a dictionary or any object).</summary>
    public object? Model { get; init; }

    /// <summary>Head metadata.</summary>
    public WebPageMeta? Meta { get; init; }

    /// <summary>The request, or null for e-mails and other offline renders.</summary>
    public HttpContext? HttpContext { get; init; }

    /// <summary>Render only the template body, ignoring its <c>{layout:…}</c> (fragments).</summary>
    public bool IgnoreLayout { get; init; }
}

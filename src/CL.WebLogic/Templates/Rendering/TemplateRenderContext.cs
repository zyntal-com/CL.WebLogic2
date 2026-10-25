using System.Text;
using CL.WebLogic.Runtime.Models;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Templates.Rendering;

/// <summary>
/// Mutable render state for one render call. Loops, partials and layouts save and restore
/// fields around nested renders instead of allocating a new context per item.
/// </summary>
public sealed class TemplateRenderContext
{
    /// <summary>Normalized path of the template rendering right now.</summary>
    public required string TemplatePath { get; set; }

    /// <summary>The model root (a dictionary or any object).</summary>
    public required object? Model { get; set; }

    /// <summary>Values every template can read (site name and app-provided globals).</summary>
    public required IReadOnlyDictionary<string, object?> Globals { get; init; }

    /// <summary>The current request, or null when rendering outside a request (e-mails, tests).</summary>
    public HttpContext? HttpContext { get; init; }

    /// <summary>Head metadata for <c>{renderhead}</c>.</summary>
    public WebPageMeta? Meta { get; init; }

    /// <summary>The buffer being written.</summary>
    public required StringBuilder Output { get; set; }

    /// <summary>Engine services.</summary>
    public required TemplateRenderServices Services { get; init; }

    /// <summary>Cancels a long render (client disconnect).</summary>
    public CancellationToken CancellationToken { get; init; }

    /// <summary>Rendered sections for the layout's <c>{rendersection}</c>.</summary>
    public IReadOnlyDictionary<string, string> Sections { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>The child body for the layout's <c>{renderbody}</c>.</summary>
    public string? RenderBody { get; set; }

    /// <summary>Current <c>{foreach}</c> item (<c>{item:…}</c>).</summary>
    public object? CurrentItem { get; set; }

    /// <summary>Current loop metadata (<c>{loop:…}</c>).</summary>
    public TemplateLoopState? Loop { get; set; }

    /// <summary>Parameters of the current partial (<c>{param:…}</c>).</summary>
    public IReadOnlyDictionary<string, object?>? Params { get; set; }

    /// <summary>Active loop aliases.</summary>
    public IReadOnlyDictionary<string, object?>? Aliases { get; set; }

    /// <summary>Templates on the current render stack (recursion guard).</summary>
    internal HashSet<string> Stack { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The per-request CSP nonce (empty outside a request).</summary>
    public string Nonce => HttpContext is null ? string.Empty : Services.Nonce.GetNonce(HttpContext);
}

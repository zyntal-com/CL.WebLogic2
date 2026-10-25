using System.Net;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Templates.Rendering;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Hosting;

namespace CL.WebLogic.Theming.Services;

/// <summary>
/// Renders browser error pages from the theme: <c>errors/{status}.html</c>, then
/// <c>errors/error.html</c>. API / JSON clients, themes without error templates and template
/// failures fall back to <see cref="DefaultErrorPageRenderer"/>.
/// Model: <c>status_code</c>, <c>reason</c>, <c>trace_id</c> and (Development only) <c>error_message</c>.
/// </summary>
public sealed class ThemedErrorPageRenderer : IWebErrorPageRenderer
{
    private readonly IWebTemplateRenderer _templates;
    private readonly DefaultErrorPageRenderer _fallback;
    private readonly IHostEnvironment _environment;
    private readonly IWebLogicContext _context;

    /// <summary>Creates the renderer.</summary>
    public ThemedErrorPageRenderer(IWebTemplateRenderer templates, DefaultErrorPageRenderer fallback, IHostEnvironment environment, IWebLogicContext context)
    {
        _templates = templates ?? throw new ArgumentNullException(nameof(templates));
        _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc/>
    public async Task RenderAsync(HttpContext httpContext, int statusCode, Exception? exception, CancellationToken cancellationToken)
    {
        var template = DefaultErrorPageRenderer.WantsProblemJson(httpContext) ? null : FindTemplate(statusCode);
        if (template is null)
        {
            await _fallback.RenderAsync(httpContext, statusCode, exception, cancellationToken).ConfigureAwait(false);
            return;
        }

        string html;
        try
        {
            html = await _templates.RenderAsync(new TemplateRenderRequest
            {
                TemplatePath = template,
                HttpContext = httpContext,
                Model = new Dictionary<string, object?>
                {
                    ["status_code"] = statusCode,
                    ["reason"] = ReasonPhrases.GetReasonPhrase(statusCode),
                    ["trace_id"] = httpContext.TraceIdentifier,
                    ["error_message"] = exception is not null && _environment.IsDevelopment() ? exception.Message : null,
                    ["page_title"] = $"{statusCode} {ReasonPhrases.GetReasonPhrase(statusCode)}"
                }
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _context.Logger.Error($"Error template '{template}' failed [{httpContext.TraceIdentifier}]: {ex.Message}", ex);
            await _fallback.RenderAsync(httpContext, statusCode, exception, cancellationToken).ConfigureAwait(false);
            return;
        }

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "text/html; charset=utf-8";
        await httpContext.Response.WriteAsync(html, cancellationToken).ConfigureAwait(false);
    }

    private string? FindTemplate(int statusCode)
    {
        var specific = "errors/" + statusCode.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (_templates.Exists(specific)) return specific;
        return _templates.Exists("errors/error") ? "errors/error" : null;
    }
}

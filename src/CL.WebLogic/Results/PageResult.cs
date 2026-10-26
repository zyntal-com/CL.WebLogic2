using CL.WebLogic.Runtime.Models;
using CL.WebLogic.Templates.Rendering;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace CL.WebLogic.Results;

/// <summary>A theme template rendered with its layout (or, for fragments, without it).</summary>
public sealed class PageResult : WebResult
{
    /// <summary>Creates the result.</summary>
    public PageResult(string templatePath, object? model, WebPageMeta? meta, bool fragment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templatePath);
        TemplatePath = templatePath;
        Model = model;
        Meta = meta;
        IsFragment = fragment;
    }

    /// <summary>Template path.</summary>
    public string TemplatePath { get; }

    /// <summary>The model.</summary>
    public object? Model { get; }

    /// <summary>Head metadata.</summary>
    public WebPageMeta? Meta { get; }

    /// <summary>True for a fragment: no layout, and marked with <c>X-WebLogic-Fragment</c> for the client.</summary>
    public bool IsFragment { get; }

    /// <inheritdoc/>
    public override async Task ExecuteAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        var renderer = request.Services.GetRequiredService<IWebTemplateRenderer>();
        var html = await renderer.RenderAsync(new TemplateRenderRequest
        {
            TemplatePath = TemplatePath,
            Model = Model,
            Meta = Meta,
            HttpContext = request.HttpContext,
            IgnoreLayout = IsFragment,
            DefaultLayout = request.Route.Layout
        }, cancellationToken).ConfigureAwait(false);

        var response = request.HttpContext.Response;
        ApplyStatusAndHeaders(response);
        if (IsFragment)
            response.Headers["X-WebLogic-Fragment"] = "1";
        response.ContentType = "text/html; charset=utf-8";
        await response.WriteAsync(html, cancellationToken).ConfigureAwait(false);
    }
}

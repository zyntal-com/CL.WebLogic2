using System.Globalization;
using System.Text.Encodings.Web;
using CL.WebLogic.Auth.Users;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Runtime.Models;
using CL.WebLogic.Templates.Rendering;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace CL.WebLogic.Results;

/// <summary>
/// A 403 telling a suspended user why and until when. Renders the theme template
/// <c>errors/suspended</c> (model: <c>reason</c>, <c>until</c>, <c>indefinite</c>) when it exists,
/// a plain built-in page otherwise, and ProblemDetails for API clients.
/// </summary>
public sealed class AccountSuspendedResult : WebResult
{
    /// <summary>The theme template looked up first.</summary>
    public const string TemplatePath = "errors/suspended";

    /// <summary>Creates the result.</summary>
    public AccountSuspendedResult(WebSuspension suspension)
    {
        Suspension = suspension ?? throw new ArgumentNullException(nameof(suspension));
        StatusCode = StatusCodes.Status403Forbidden;
    }

    /// <summary>The suspension shown.</summary>
    public WebSuspension Suspension { get; }

    /// <inheritdoc/>
    public override async Task ExecuteAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var until = Suspension.IsIndefinite ? null : Suspension.Until.ToUniversalTime().ToString("u", CultureInfo.InvariantCulture);
        if (DefaultErrorPageRenderer.WantsProblemJson(request.HttpContext))
        {
            var problem = new ProblemResult(StatusCode, "Account suspended", Suspension.Reason, "about:blank#account-suspended");
            problem.Details.Extensions["suspendedUntil"] = until;
            await problem.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
            return;
        }

        var renderer = request.Services.GetRequiredService<IWebTemplateRenderer>();
        string html;
        if (renderer.Exists(TemplatePath))
        {
            html = await renderer.RenderAsync(new TemplateRenderRequest
            {
                TemplatePath = TemplatePath,
                HttpContext = request.HttpContext,
                Model = new Dictionary<string, object?>
                {
                    ["reason"] = Suspension.Reason,
                    ["until"] = Suspension.IsIndefinite ? null : Suspension.Until,
                    ["indefinite"] = Suspension.IsIndefinite,
                    ["page_title"] = "Account suspended"
                }
            }, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var encoder = HtmlEncoder.Default;
            html = "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><title>Account suspended</title></head><body>"
                + "<h1>Account suspended</h1>"
                + (Suspension.Reason is { Length: > 0 } reason ? "<p>" + encoder.Encode(reason) + "</p>" : "")
                + "<p>" + (until is null ? "The suspension has no end date." : "It ends " + encoder.Encode(until) + ".") + "</p>"
                + "</body></html>";
        }

        var response = request.HttpContext.Response;
        ApplyStatusAndHeaders(response);
        response.Headers.CacheControl = "no-store";
        response.ContentType = "text/html; charset=utf-8";
        await response.WriteAsync(html, cancellationToken).ConfigureAwait(false);
    }
}

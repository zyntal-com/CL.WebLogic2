using System.Text.Json;
using CL.WebLogic.Forms.Models;
using CL.WebLogic.Runtime.Models;
using CL.WebLogic.Runtime.Services;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Results;

/// <summary>
/// The answer to a form post. For the WebLogic client (or JSON callers) it is the JSON form
/// result <c>{ ok, formId, errors, summary, message, redirect, commands, data, step }</c>
/// (status 200, or 422 for errors). Plain browser posts get a 303 redirect on success (the message
/// travels to the next page as a <see cref="WebFlash"/> toast) and the fallback page (422) on errors.
/// Without a fallback template they go back to the posting page (a Referer on this site) with the
/// errors as a toast, or get a plain 422 text answer when there is no such page.
/// </summary>
public sealed class FormResult : WebResult
{
    private FormResult()
    {
    }

    /// <summary>Success.</summary>
    public bool Ok { get; private init; }

    /// <summary>Form id.</summary>
    public string? FormId { get; private init; }

    /// <summary>Field errors.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Errors { get; private init; } = new Dictionary<string, IReadOnlyList<string>>();

    /// <summary>Form-level message for errors.</summary>
    public string? Summary { get; private init; }

    /// <summary>Success message (shown as a toast).</summary>
    public string? Message { get; private init; }

    /// <summary>Local URL to go to.</summary>
    public string? RedirectUrl { get; private init; }

    /// <summary>Commands for the client.</summary>
    public IReadOnlyList<WebCommand> ClientCommands { get; private init; } = [];

    /// <summary>Extra data.</summary>
    public object? Data { get; private init; }

    /// <summary>Wizard step now shown.</summary>
    public string? CurrentStep { get; private init; }

    private string? FallbackTemplate { get; init; }

    private string? StepHtml { get; init; }

    private object? FallbackModel { get; init; }

    /// <summary>A successful submission.</summary>
    public static FormResult Success(string? message = null, string? redirect = null, object? data = null, params WebCommand[] commands)
    {
        if (redirect is not null && !WebUrl.IsLocal(redirect))
            throw new ArgumentException($"'{redirect}' is not a local URL.", nameof(redirect));
        return new FormResult { Ok = true, Message = message, RedirectUrl = redirect, Data = data, ClientCommands = commands };
    }

    /// <summary>A submission with errors.</summary>
    public static FormResult Failure(string formId, WebFormErrors errors, string? summary = null, string? fallbackTemplate = null, object? fallbackModel = null)
    {
        ArgumentNullException.ThrowIfNull(errors);
        return new FormResult
        {
            Ok = false,
            FormId = formId,
            Errors = errors.Fields,
            Summary = summary ?? (errors.Form.Count > 0 ? string.Join(' ', errors.Form) : null),
            FallbackTemplate = fallbackTemplate,
            FallbackModel = fallbackModel,
            StatusCode = StatusCodes.Status422UnprocessableEntity
        };
    }

    /// <summary>
    /// The next wizard step: the client replaces the form element with <paramref name="html"/>;
    /// plain posts get <paramref name="fallbackTemplate"/> (whose <c>{form:}</c> shows the step) or the bare form.
    /// </summary>
    public static FormResult Step(string formId, string step, string html, string? fallbackTemplate = null, object? fallbackModel = null) => new()
    {
        Ok = true,
        FormId = formId,
        CurrentStep = step,
        StepHtml = html,
        FallbackTemplate = fallbackTemplate,
        FallbackModel = fallbackModel,
        ClientCommands = [WebCommand.Replace("#wl-form-" + formId, html)]
    };

    /// <inheritdoc/>
    public override async Task ExecuteAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var accept = request.HttpContext.Request.Headers.Accept.ToString();
        if (request.IsWebLogicClient || accept.Contains("application/json", StringComparison.OrdinalIgnoreCase))
        {
            var response = request.HttpContext.Response;
            ApplyStatusAndHeaders(response);
            response.ContentType = "application/json; charset=utf-8";
            var payload = new Dictionary<string, object?>
            {
                ["ok"] = Ok,
                ["formId"] = FormId,
                ["errors"] = Errors,
                ["summary"] = Summary,
                ["title"] = Ok ? null : Summary ?? "Please correct the highlighted fields.",
                ["message"] = Message,
                ["redirect"] = RedirectUrl,
                ["commands"] = ClientCommands.Select(c => c.ToPayload()).ToList(),
                ["data"] = Data,
                ["step"] = CurrentStep
            };
            await JsonSerializer.SerializeAsync(response.Body, payload, request.JsonOptions, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (CurrentStep is not null)
        {
            WebResult step = FallbackTemplate is not null ? Page(FallbackTemplate, FallbackModel) : Html(StepHtml ?? "");
            await step.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
            return;
        }

        var flash = request.GetService<WebFlash>();
        if (Ok)
        {
            flash?.Set(request.HttpContext, Message, "success");
            await new RedirectResult(RedirectUrl ?? (request.Path + request.QueryString), permanent: false, preserveMethod: false) { StatusCode = StatusCodes.Status303SeeOther }
                .ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
            return;
        }

        var problem = Summary ?? string.Join('\n', Errors.SelectMany(e => e.Value));
        if (FallbackTemplate is null && flash is not null && LocalReferer(request) is { } back)
        {
            flash.Set(request.HttpContext, problem.Length > 0 ? problem : "Please correct the highlighted fields.", "danger");
            await new RedirectResult(back, permanent: false, preserveMethod: false) { StatusCode = StatusCodes.Status303SeeOther }
                .ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
            return;
        }

        WebResult fallback = FallbackTemplate is not null
            ? Page(FallbackTemplate, FallbackModel, statusCode: StatusCodes.Status422UnprocessableEntity)
            : Text(problem, StatusCodes.Status422UnprocessableEntity);
        await fallback.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The Referer as a local path when it is a page of this site (same scheme and host), else null.</summary>
    private static string? LocalReferer(WebRequestContext request)
    {
        var http = request.HttpContext.Request;
        if (!Uri.TryCreate(request.GetHeader("Referer"), UriKind.Absolute, out var referer)
            || !string.Equals(referer.Scheme, http.Scheme, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(referer.Authority, http.Host.Value, StringComparison.OrdinalIgnoreCase))
            return null;
        var local = referer.PathAndQuery;
        return WebUrl.IsLocal(local) ? local : null;
    }
}

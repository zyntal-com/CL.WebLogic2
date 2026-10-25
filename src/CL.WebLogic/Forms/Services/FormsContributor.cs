using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Forms.Providers;
using CL.WebLogic.Results;
using CL.WebLogic.Routing;
using CL.WebLogic.Routing.Models;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Runtime.Models;
using CL.WebLogic.Security.Authorization;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Forms.Services;

/// <summary>
/// Form endpoints, always registered: remote field validation
/// (<c>POST /_weblogic/forms/{form}/validate/{field}</c>, CSRF-protected) and options/search
/// (<c>GET /_weblogic/forms/options/{provider}?q=</c>, access decided per provider). Both are rate limited.
/// </summary>
public sealed class FormsContributor : IWebContributor
{
    /// <summary>Contributor id.</summary>
    public const string ContributorId = "weblogic.forms";

    private const int MaxTermLength = 100;

    private readonly WebFormRegistry _forms;
    private readonly WebFormService _service;
    private readonly WebFormOptionsRegistry _options;
    private readonly IWebAuthorizationService _authorization;
    private readonly WebLogicConfigHolder _config;

    /// <summary>Creates the contributor.</summary>
    public FormsContributor(WebFormRegistry forms, WebFormService service, WebFormOptionsRegistry options, IWebAuthorizationService authorization, WebLogicConfigHolder config)
    {
        _forms = forms ?? throw new ArgumentNullException(nameof(forms));
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>Descriptor.</summary>
    public static WebContributorDescriptor Descriptor { get; } = new()
    {
        Id = ContributorId,
        Name = "WebLogic forms",
        Kind = "System",
        Description = "Remote field validation and form options/search endpoints."
    };

    /// <inheritdoc/>
    public Task ContributeAsync(WebContributionBuilder builder, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var ratePolicy = _config.Current.Forms.EndpointRatePolicy;

        builder.MapApi("/_weblogic/forms/{form}/validate/{field}", ValidateAsync, "POST")
            .AllowAnonymous()
            .WithRateLimit(ratePolicy)
            .WithName("weblogic.forms.validate")
            .WithDescription("Validates one field of a registered form");

        builder.MapApi("/_weblogic/forms/options/{provider}", OptionsAsync, "GET")
            .AllowAnonymous()
            .WithRateLimit(ratePolicy)
            .WithName("weblogic.forms.options")
            .WithDescription("Options and search results of a form options provider (access per provider)");
        return Task.CompletedTask;
    }

    private async ValueTask<WebResult> ValidateAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        // Only fields marked [WebField(Remote = true)] answer: the endpoint is anonymous, so every
        // other field (and the form validators behind it) stays unreachable.
        var type = _forms.Find(request.GetRouteValue("form"));
        var field = request.GetRouteValue("field");
        if (type is null || string.IsNullOrEmpty(field) || _service.Definitions.Get(type).Field(field) is not { Remote: true } target)
            return WebResult.Problem(StatusCodes.Status404NotFound, "Unknown form or field");
        if (target.OptionsProvider is not null && _options.Get(target.OptionsProvider) is { } provider
            && await DenyAsync(request, provider, cancellationToken).ConfigureAwait(false) is { } denied)
            return denied;

        var errors = await _service.ValidateFieldAsync(type, field, request, cancellationToken).ConfigureAwait(false);
        return WebResult.Json(new { valid = errors.Count == 0, errors });
    }

    private async ValueTask<WebResult> OptionsAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        var provider = _options.Get(request.GetRouteValue("provider"));
        if (provider is null)
            return WebResult.Problem(StatusCodes.Status404NotFound, "Unknown options provider");

        if (await DenyAsync(request, provider, cancellationToken).ConfigureAwait(false) is { } denied)
            return denied;

        var context = new WebFormOptionsRequest
        {
            Identity = request.Identity,
            Security = request.Security,
            FormId = Limit(request.GetQuery("form")),
            Field = Limit(request.GetQuery("field"))
        };
        var term = request.GetQuery("q");
        var options = term is null
            ? await provider.GetOptionsAsync(context, cancellationToken).ConfigureAwait(false)
            : term.Length > MaxTermLength ? [] : await provider.SearchAsync(term.Trim(), context, cancellationToken).ConfigureAwait(false);

        return WebResult.Json(new
        {
            options = options.Take(term is null ? 1000 : 50).Select(o => new { value = o.Value, label = o.Label, disabled = o.Disabled, group = o.Group })
        });
    }

    /// <summary>The provider's own access rule (anonymous flag, policy); null when allowed.</summary>
    private async ValueTask<WebResult?> DenyAsync(WebRequestContext request, IWebFormOptionsProvider provider, CancellationToken cancellationToken)
    {
        if (!provider.AllowAnonymous && !request.Identity.IsAuthenticated)
            return WebResult.Problem(StatusCodes.Status401Unauthorized, "Sign in to use this lookup");
        if (provider.Policy is null)
            return null;
        var result = await _authorization.AuthorizeAsync(request.Security, provider.Policy, null, cancellationToken).ConfigureAwait(false);
        return result.Succeeded ? null : WebResult.Problem(result.Challenge ? StatusCodes.Status401Unauthorized : StatusCodes.Status403Forbidden);
    }

    private static string? Limit(string? value) => value is { Length: > 0 and <= 128 } ? value : null;
}

using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Forms.Models;
using CL.WebLogic.Forms.Providers;
using CL.WebLogic.Forms.Rendering;
using CL.WebLogic.Runtime.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace CL.WebLogic.Forms.Services;

public sealed partial class WebFormService
{
    /// <summary>Renders a form with the state remembered for this request (or <paramref name="state"/>).</summary>
    public Task<string> RenderAsync(WebRequestContext request, WebFormDefinition definition, WebFormRenderState? state, WebFormRenderOptions? options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return RenderAsync(request.HttpContext, definition, state, options, cancellationToken);
    }

    /// <summary>Renders a form for <paramref name="httpContext"/>.</summary>
    public async Task<string> RenderAsync(HttpContext httpContext, WebFormDefinition definition, WebFormRenderState? state, WebFormRenderOptions? options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(definition);
        state ??= httpContext.Items.TryGetValue(WebFormRenderState.ItemKey(definition.Id), out var remembered) ? remembered as WebFormRenderState : null;
        state ??= new WebFormRenderState();

        var rendererName = options?.Renderer ?? httpContext.RequestServices.GetRequiredService<WebLogicConfigHolder>().Current.Forms.Renderer;
        var renderer = httpContext.RequestServices.GetServices<IWebFormRenderer>().LastOrDefault(r => string.Equals(r.Name, rendererName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"No form renderer named '{rendererName}' is registered.");

        var providerOptions = await LoadProviderOptionsAsync(httpContext, definition, cancellationToken).ConfigureAwait(false);
        return await renderer.RenderAsync(new WebFormRenderRequest
        {
            Definition = definition,
            HttpContext = httpContext,
            Action = options?.Action,
            CssClass = options?.CssClass,
            SubmitLabel = options?.SubmitLabel,
            Values = state.Values,
            Errors = state.Errors,
            StepIndex = state.StepIndex,
            WizardToken = state.WizardToken,
            ProviderOptions = providerOptions
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Remembers values to show the next time the form renders in this request (edit forms).</summary>
    public void Prefill<TModel>(HttpContext httpContext, TModel model)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        var definition = _definitions.Get<TModel>();
        httpContext.Items[WebFormRenderState.ItemKey(definition.Id)] = new WebFormRenderState { Values = WebFormValues.FromModel(definition, model) };
    }

    private async Task<IReadOnlyDictionary<string, IReadOnlyList<WebFieldOption>>> LoadProviderOptionsAsync(HttpContext httpContext, WebFormDefinition definition, CancellationToken cancellationToken)
    {
        var loaded = new Dictionary<string, IReadOnlyList<WebFieldOption>>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in definition.Fields)
        {
            if (field.OptionsProvider is null || field.Options.Count > 0 || field.Type is WebFieldType.Autocomplete or WebFieldType.Text)
                continue;
            if (_options.Get(field.OptionsProvider) is not { } provider)
            {
                _context.Logger.Warning($"Form '{definition.Id}': options provider '{field.OptionsProvider}' is not registered.");
                continue;
            }
            var request = new WebFormOptionsRequest { FormId = definition.Id, Field = field.Name, Identity = Security.Pipeline.WebSecurityContext.Get(httpContext)?.Identity ?? Security.Identity.WebIdentity.Anonymous, Security = Security.Pipeline.WebSecurityContext.Get(httpContext) };
            loaded[field.Name] = await provider.GetOptionsAsync(request, cancellationToken).ConfigureAwait(false);
        }
        return loaded;
    }
}

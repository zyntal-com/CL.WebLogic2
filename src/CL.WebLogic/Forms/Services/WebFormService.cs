using CL.WebLogic.Forms.AntiBot;
using CL.WebLogic.Forms.Events;
using CL.WebLogic.Forms.Models;
using CL.WebLogic.Forms.Providers;
using CL.WebLogic.Forms.Rendering;
using CL.WebLogic.Forms.Uploads;
using CL.WebLogic.Forms.Validation;
using CL.WebLogic.Forms.Wizards;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Runtime.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

namespace CL.WebLogic.Forms.Services;

/// <summary>
/// The form pipeline for a post: anti-bot checks, wizard state, binding, upload checks, field
/// rules, option-provider checks and <see cref="IWebFormValidator{TModel}"/>s.
/// </summary>
public sealed partial class WebFormService
{
    private readonly WebFormDefinitionProvider _definitions;
    private readonly WebFormBinder _binder;
    private readonly WebFieldValidator _validator;
    private readonly WebUploadChecker _uploads;
    private readonly WebFormAntiBot _antiBot;
    private readonly WebWizardState _wizard;
    private readonly WebFormOptionsRegistry _options;
    private readonly WebFormMessages _messages;
    private readonly IWebLogicContext _context;

    /// <summary>Creates the service.</summary>
    public WebFormService(WebFormDefinitionProvider definitions, WebFormBinder binder, WebFieldValidator validator, WebUploadChecker uploads,
        WebFormAntiBot antiBot, WebWizardState wizard, WebFormOptionsRegistry options, WebFormMessages messages, IWebLogicContext context)
    {
        _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
        _binder = binder ?? throw new ArgumentNullException(nameof(binder));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _uploads = uploads ?? throw new ArgumentNullException(nameof(uploads));
        _antiBot = antiBot ?? throw new ArgumentNullException(nameof(antiBot));
        _wizard = wizard ?? throw new ArgumentNullException(nameof(wizard));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _messages = messages ?? throw new ArgumentNullException(nameof(messages));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>The definitions.</summary>
    public WebFormDefinitionProvider Definitions => _definitions;

    /// <summary>Binds and validates the posted form as <typeparamref name="TModel"/>.</summary>
    public async Task<WebFormSubmission<TModel>> BindAsync<TModel>(WebRequestContext request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var definition = _definitions.Get<TModel>();
        var form = await request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
        var errors = new WebFormErrors();

        if (definition.AntiBot && _antiBot.Check(definition.Id, form) is { } reason)
        {
            _context.Logger.Info($"Form '{definition.Id}' rejected by the anti-bot checks ({reason}).");
            _context.Events.Publish(new FormBotRejectedEvent(definition.Id, reason, request.Path));
            errors.Add(string.Empty, _messages.Template("rejected"));
            var rejected = new WebFormSubmission<TModel>(definition, Activator.CreateInstance<TModel>(), errors, new Dictionary<string, StringValues>()) { Rejected = true };
            Remember(request, rejected);
            return rejected;
        }

        var (values, stepIndex, back) = CollectValues(definition, form, request);
        var steps = definition.Steps;
        var lastStep = steps.Count == 0 || stepIndex >= steps.Count - 1;
        var visible = WebFormVisibility.For(definition, values);
        var fields = lastStep ? definition.Fields : [.. definition.FieldsOf(steps[stepIndex].Name)];

        var model = (TModel)_binder.Bind(definition, definition.Fields, values, visible, errors);
        WebFormSubmission<TModel> submission;
        if (back)
        {
            errors = new WebFormErrors();
            submission = new WebFormSubmission<TModel>(definition, model, errors, values) { IsComplete = false, StepIndex = Math.Max(0, stepIndex - 1) };
        }
        else
        {
            if (form.Files.Count > 0)
                await BindFilesAsync(definition.FieldsOf(steps.Count == 0 ? null : steps[stepIndex].Name), "", form.Files, model!, visible, errors, cancellationToken).ConfigureAwait(false);
            _validator.Validate(definition, fields, model!, visible, errors);
            if (!errors.Any)
                await CheckProviderChoicesAsync(definition, fields, values, visible, request, errors, cancellationToken).ConfigureAwait(false);
            if (!errors.Any && lastStep)
                await RunValidatorsAsync(definition, model, request, null, errors, cancellationToken).ConfigureAwait(false);

            submission = new WebFormSubmission<TModel>(definition, model, errors, values) { StepIndex = stepIndex, IsComplete = lastStep };
            if (!errors.Any && !lastStep)
            {
                submission.IsComplete = false;
                submission.StepIndex = stepIndex + 1;
            }
        }

        if (steps.Count > 0)
            submission.WizardToken = _wizard.Protect(definition.Id, submission.StepIndex, values);
        Remember(request, submission);
        if (!submission.IsComplete && submission.IsValid)
            submission.StepHtml = await RenderAsync(request, definition, State(submission), null, cancellationToken).ConfigureAwait(false);
        _context.Events.Publish(new FormSubmittedEvent(definition.Id, submission.IsValid, request.Path));
        return submission;
    }

    /// <summary>Validates one field of a posted form (remote validation): field rules, then the form validators limited to that field.</summary>
    public async Task<IReadOnlyList<string>> ValidateFieldAsync(Type modelType, string field, WebRequestContext request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var definition = _definitions.Get(modelType);
        var target = definition.Field(field);
        if (target is null || target.Type == WebFieldType.File)
            return [];
        var form = await request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
        var values = form.Where(p => !IsInternal(p.Key, request)).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var visible = WebFormVisibility.For(definition, values);
        var errors = new WebFormErrors();
        var model = _binder.Bind(definition, definition.Fields, values, visible, errors);
        _validator.Validate(definition, [target], model, visible, errors);
        if (!errors.Has(target.Name))
            await CheckProviderChoicesAsync(definition, [target], values, visible, request, errors, cancellationToken).ConfigureAwait(false);
        if (!errors.Has(target.Name))
            await RunValidatorsAsync(definition, model, request, target.Name, errors, cancellationToken).ConfigureAwait(false);
        return errors.For(target.Name);
    }

    private (Dictionary<string, StringValues> Values, int Step, bool Back) CollectValues(WebFormDefinition definition, IFormCollection form, WebRequestContext request)
    {
        var posted = form.Where(p => !IsInternal(p.Key, request)).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        if (definition.Steps.Count == 0)
            return (posted, 0, false);

        var state = _wizard.Read(definition.Id, form[WebWizardState.TokenField].ToString());
        var step = state is null ? 0 : Math.Clamp(state.Value.Step, 0, definition.Steps.Count - 1);
        var values = state?.Values ?? new Dictionary<string, StringValues>(StringComparer.Ordinal);
        // Only the current step's fields come from the post; earlier steps come from the protected state.
        foreach (var field in definition.FieldsOf(definition.Steps[step].Name))
        {
            foreach (var key in values.Keys.Where(k => k == field.Name || k.StartsWith(field.Name + "[", StringComparison.Ordinal)).ToList())
                values.Remove(key);
            foreach (var (key, value) in posted.Where(p => p.Key == field.Name || p.Key.StartsWith(field.Name + "[", StringComparison.Ordinal)))
                values[key] = value;
        }
        return (values, step, form.ContainsKey(WebWizardState.BackField));
    }

    private static bool IsInternal(string key, WebRequestContext request) =>
        key.StartsWith("__wl_", StringComparison.Ordinal)
        || string.Equals(key, request.GetRequiredService<Configuration.Services.WebLogicConfigHolder>().Current.Security.Csrf.FieldName, StringComparison.Ordinal);

    private async Task BindFilesAsync(IEnumerable<WebFieldDefinition> fields, string prefix, IFormFileCollection files, object model,
        Func<string, bool> visible, WebFormErrors errors, CancellationToken cancellationToken)
    {
        foreach (var field in fields)
        {
            var key = prefix + field.Name;
            if (field.Type == WebFieldType.File && visible(key) && !field.ReadOnly)
                await _uploads.BindAsync(field, key, files, model, errors, cancellationToken).ConfigureAwait(false);
            else if (field.Repeater is not null && field.Property.GetValue(model) is System.Collections.IList items)
            {
                for (var i = 0; i < items.Count; i++)
                {
                    if (items[i] is { } item)
                        await BindFilesAsync(field.Repeater.Fields, $"{key}[{i}].", files, item, visible, errors, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    private async Task CheckProviderChoicesAsync(WebFormDefinition definition, IEnumerable<WebFieldDefinition> fields, Dictionary<string, StringValues> values,
        Func<string, bool> visible, WebRequestContext request, WebFormErrors errors, CancellationToken cancellationToken)
    {
        foreach (var field in fields)
        {
            if (field.OptionsProvider is null || field.Options.Count > 0 || field.Type is WebFieldType.Autocomplete or WebFieldType.Text || !visible(field.Name))
                continue;
            var provider = _options.Get(field.OptionsProvider);
            if (provider is null || !values.TryGetValue(field.Name, out var posted))
                continue;
            var context = new WebFormOptionsRequest { Identity = request.Identity, Security = request.Security, FormId = definition.Id, Field = field.Name };
            foreach (var value in posted.Where(v => !string.IsNullOrWhiteSpace(v)))
            {
                if (!await provider.IsValidAsync(value!.Trim(), context, cancellationToken).ConfigureAwait(false))
                {
                    errors.Add(field.Name, _messages.Format("choice", field.Label));
                    break;
                }
            }
        }
    }

    private static async Task RunValidatorsAsync<TModel>(WebFormDefinition definition, TModel model, WebRequestContext request, string? onlyField, WebFormErrors errors, CancellationToken cancellationToken)
    {
        var context = new WebFormValidationContext(definition, errors, request, onlyField);
        foreach (var validator in request.Services.GetServices<IWebFormValidator<TModel>>())
            await validator.ValidateAsync(model, context, cancellationToken).ConfigureAwait(false);
    }

    private static Task RunValidatorsAsync(WebFormDefinition definition, object model, WebRequestContext request, string? onlyField, WebFormErrors errors, CancellationToken cancellationToken)
    {
        var method = typeof(WebFormService).GetMethods(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            .First(m => m.Name == nameof(RunValidatorsAsync) && m.IsGenericMethodDefinition)
            .MakeGenericMethod(definition.ModelType);
        return (Task)method.Invoke(null, [definition, model, request, onlyField, errors, cancellationToken])!;
    }

    private static WebFormRenderState State<TModel>(WebFormSubmission<TModel> submission) => new()
    {
        Values = submission.Values,
        Errors = submission.Errors,
        StepIndex = submission.StepIndex,
        WizardToken = submission.WizardToken
    };

    private static void Remember<TModel>(WebRequestContext request, WebFormSubmission<TModel> submission) =>
        request.HttpContext.Items[WebFormRenderState.ItemKey(submission.Definition.Id)] = State(submission);
}

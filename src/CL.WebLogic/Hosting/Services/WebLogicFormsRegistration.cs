using CL.WebLogic.Forms.AntiBot;
using CL.WebLogic.Forms.Providers;
using CL.WebLogic.Forms.Rendering;
using CL.WebLogic.Forms.Services;
using CL.WebLogic.Forms.Uploads;
using CL.WebLogic.Forms.Validation;
using CL.WebLogic.Forms.Wizards;
using CL.WebLogic.Templates.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CL.WebLogic.Hosting.Services;

/// <summary>Registers the forms services (called by <c>AddCodeLogicWebLogic</c>).</summary>
internal static class WebLogicFormsRegistration
{
    /// <summary>Adds the forms services.</summary>
    public static void AddForms(IServiceCollection services)
    {
        services.TryAddSingleton<WebFormMessages>();
        services.TryAddSingleton<WebFormDefinitionProvider>();
        services.TryAddSingleton<WebFormBinder>();
        services.TryAddSingleton<WebFormMapper>();
        services.TryAddSingleton<WebFieldValidator>();
        services.TryAddSingleton<WebJsonValidator>();
        services.TryAddSingleton<WebUploadChecker>();
        services.TryAddSingleton<WebFormAntiBot>();
        services.TryAddSingleton<WebWizardState>();
        services.TryAddSingleton<WebFormOptionsRegistry>();
        services.TryAddSingleton<WebFormRegistry>();
        services.TryAddSingleton<WebFormService>();
        services.TryAddSingleton<WebFormRendererServices>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IWebFormRenderer, Bootstrap5FormRenderer>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IWebFormRenderer, PlainFormRenderer>());
        services.TryAddSingleton<ITemplateFormRenderer, WebFormTemplateRenderer>();
        services.TryAddSingleton<IWebUploadSink, StorageUploadSink>();
        services.TryAddSingleton<FormsContributor>();
    }
}

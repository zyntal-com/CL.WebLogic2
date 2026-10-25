using CL.WebLogic.Forms.Models;
using CL.WebLogic.Forms.Providers;
using CL.WebLogic.Forms.Rendering;
using CL.WebLogic.Forms.Services;
using CL.WebLogic.Forms.Uploads;
using CL.WebLogic.Forms.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CL.WebLogic.Hosting.Services;

/// <summary>Forms configuration on <see cref="WebLogicBuilder"/>.</summary>
public static class WebLogicFormsBuilderExtensions
{
    /// <summary>Registers a form model so <c>{form:id}</c> and remote validation can find it.</summary>
    public static WebLogicBuilder AddForm<TModel>(this WebLogicBuilder builder) where TModel : class, new()
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddSingleton(new WebFormRegistration(typeof(TModel)));
        return builder;
    }

    /// <summary>Configures a form in code (applied after its attributes).</summary>
    public static WebLogicBuilder ConfigureForm<TModel, TConfigurator>(this WebLogicBuilder builder)
        where TConfigurator : class, IWebFormConfigurator<TModel>
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddSingleton<IWebFormConfigurator<TModel>, TConfigurator>();
        return builder;
    }

    /// <summary>Adds a cross-field / async validator for a form.</summary>
    public static WebLogicBuilder AddFormValidator<TModel, TValidator>(this WebLogicBuilder builder)
        where TValidator : class, IWebFormValidator<TModel>
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddScoped<IWebFormValidator<TModel>, TValidator>();
        return builder;
    }

    /// <summary>Adds an options provider resolved from DI.</summary>
    public static WebLogicBuilder AddFormOptions<TProvider>(this WebLogicBuilder builder) where TProvider : class, IWebFormOptionsProvider
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddSingleton<IWebFormOptionsProvider, TProvider>();
        return builder;
    }

    /// <summary>Adds a fixed options list (anonymous access by default).</summary>
    public static WebLogicBuilder AddFormOptions(this WebLogicBuilder builder, string name, IReadOnlyList<WebFieldOption> options, bool allowAnonymous = true)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddSingleton<IWebFormOptionsProvider>(new StaticOptionsProvider(name, options, allowAnonymous));
        return builder;
    }

    /// <summary>Adds a custom form renderer (select it with <c>Forms.Renderer</c> or <c>{form:id renderer="name"}</c>).</summary>
    public static WebLogicBuilder AddFormRenderer<TRenderer>(this WebLogicBuilder builder) where TRenderer : class, IWebFormRenderer
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddSingleton<IWebFormRenderer, TRenderer>();
        return builder;
    }

    /// <summary>Replaces where uploads are saved.</summary>
    public static WebLogicBuilder UseUploadSink<TSink>(this WebLogicBuilder builder) where TSink : class, IWebUploadSink
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.Replace(ServiceDescriptor.Singleton<IWebUploadSink, TSink>());
        return builder;
    }
}

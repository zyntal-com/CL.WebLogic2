using System.Runtime.CompilerServices;
using CL.WebLogic.Forms.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CL.WebLogic.Forms.Services;

/// <summary>
/// Definitions per model type: built from attributes, then configured by registered
/// <see cref="IWebFormConfigurator{TModel}"/>s; cached weakly so plugin types can unload.
/// </summary>
public sealed class WebFormDefinitionProvider
{
    private readonly ConditionalWeakTable<Type, WebFormDefinition> _cache = [];
    private readonly IServiceProvider _services;

    /// <summary>Creates the provider.</summary>
    public WebFormDefinitionProvider(IServiceProvider services) => _services = services ?? throw new ArgumentNullException(nameof(services));

    /// <summary>The definition of <typeparamref name="TModel"/>.</summary>
    public WebFormDefinition Get<TModel>() => Get(typeof(TModel));

    /// <summary>The definition of <paramref name="modelType"/>.</summary>
    public WebFormDefinition Get(Type modelType) => _cache.GetValue(modelType, Create);

    private WebFormDefinition Create(Type modelType)
    {
        var definition = WebFormDefinitionBuilder.Build(modelType);
        Configure(definition);
        return definition;
    }

    private void Configure(WebFormDefinition definition)
    {
        var configuratorType = typeof(IWebFormConfigurator<>).MakeGenericType(definition.ModelType);
        var builderType = typeof(WebFormBuilder<>).MakeGenericType(definition.ModelType);
        var configurators = _services.GetServices(configuratorType).ToList();
        if (configurators.Count > 0)
        {
            var builder = Activator.CreateInstance(builderType, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, [definition], null)!;
            var configure = configuratorType.GetMethod(nameof(IWebFormConfigurator<object>.Configure))!;
            foreach (var configurator in configurators)
                configure.Invoke(configurator, [builder]);
            definition.Fields = [.. definition.Fields.OrderBy(f => f.Order)];
        }
        foreach (var field in definition.Fields.Where(f => f.Repeater is not null))
            Configure(field.Repeater!);
    }
}

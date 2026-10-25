using CL.WebLogic.Forms.Providers;
using CL.WebLogic.Routing.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CL.WebLogic.Forms.Services;

/// <summary>
/// Form registrations from contributors and plugins (the startup equivalents are
/// <c>web.AddForm&lt;T&gt;()</c> / <c>web.AddFormOptions(…)</c>). They are added when the
/// contribution commits and removed with it; taking a name that is already registered fails the
/// contribution, so a plugin cannot replace an application's form.
/// </summary>
public static class WebFormContributionExtensions
{
    /// <summary>Registers <typeparamref name="TModel"/> for <c>{form:id}</c> and the remote-validation endpoint.</summary>
    public static string MapForm<TModel>(this WebContributionBuilder builder) where TModel : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        var id = WebFormDefinitionBuilder.Build(typeof(TModel)).Id;
        var registry = builder.Services.GetRequiredService<WebFormRegistry>();
        builder.OnCommit(() =>
        {
            if (registry.Find(id) is not null)
                throw new InvalidOperationException($"A form named '{id}' is already registered.");
            registry.Register(typeof(TModel));
        });
        builder.OnRemove(() =>
        {
            if (registry.Find(id) == typeof(TModel))
                registry.Unregister(id);
            return ValueTask.CompletedTask;
        });
        return id;
    }

    /// <summary>Registers an options provider (select/radio/autocomplete values, <c>/_weblogic/forms/options/{name}</c>).</summary>
    public static void MapFormOptions(this WebContributionBuilder builder, IWebFormOptionsProvider provider)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(provider);
        var registry = builder.Services.GetRequiredService<WebFormOptionsRegistry>();
        builder.OnCommit(() =>
        {
            if (registry.Get(provider.Name) is not null)
                throw new InvalidOperationException($"A form options provider named '{provider.Name}' is already registered.");
            registry.Register(provider);
        });
        builder.OnRemove(() =>
        {
            if (ReferenceEquals(registry.Get(provider.Name), provider))
                registry.Unregister(provider.Name);
            return ValueTask.CompletedTask;
        });
    }
}

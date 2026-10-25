using System.Collections.Concurrent;

namespace CL.WebLogic.Forms.Services;

/// <summary>
/// Forms by id, for <c>{form:id}</c> and the remote-validation endpoint. Register with
/// <c>web.AddForm&lt;T&gt;()</c> or at runtime (plugins: register on load, unregister on unload).
/// </summary>
public sealed class WebFormRegistry
{
    private readonly ConcurrentDictionary<string, Type> _forms = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Creates the registry with the forms registered in DI.</summary>
    public WebFormRegistry(IEnumerable<WebFormRegistration> registrations)
    {
        foreach (var registration in registrations ?? [])
            Register(registration.ModelType);
    }

    /// <summary>Registers <paramref name="modelType"/> under its form id.</summary>
    public void Register(Type modelType)
    {
        ArgumentNullException.ThrowIfNull(modelType);
        _forms[WebFormDefinitionBuilder.Build(modelType).Id] = modelType;
    }

    /// <summary>Removes a form.</summary>
    public bool Unregister(string formId) => _forms.TryRemove(formId, out _);

    /// <summary>The model type of a form id.</summary>
    public Type? Find(string? formId) => formId is not null && _forms.TryGetValue(formId, out var type) ? type : null;

    /// <summary>All registered forms.</summary>
    public IReadOnlyDictionary<string, Type> All => new Dictionary<string, Type>(_forms, StringComparer.OrdinalIgnoreCase);
}

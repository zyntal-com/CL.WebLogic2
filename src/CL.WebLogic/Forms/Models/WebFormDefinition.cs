namespace CL.WebLogic.Forms.Models;

/// <summary>A form built from a model type: fields in order, steps, id and texts.</summary>
public sealed class WebFormDefinition
{
    private Dictionary<string, WebFieldDefinition>? _byName;

    /// <summary>Form id (from <c>[WebForm]</c> or the type name in kebab-case).</summary>
    public required string Id { get; init; }

    /// <summary>The model type.</summary>
    public required Type ModelType { get; init; }

    /// <summary>Title.</summary>
    public string? Title { get; set; }

    /// <summary>Submit button text.</summary>
    public string SubmitLabel { get; set; } = "Submit";

    /// <summary>Fields, ordered.</summary>
    public IReadOnlyList<WebFieldDefinition> Fields { get; set; } = [];

    /// <summary>Wizard steps, ordered (empty for single-step forms).</summary>
    public IReadOnlyList<WebFormStep> Steps { get; set; } = [];

    /// <summary>Apply the anti-bot checks (honeypot, timing).</summary>
    public bool AntiBot { get; set; } = true;

    /// <summary>A field by name.</summary>
    public WebFieldDefinition? Field(string name)
    {
        _byName ??= Fields.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
        return _byName.GetValueOrDefault(name);
    }

    /// <summary>Fields of a step (all fields when there are no steps).</summary>
    public IEnumerable<WebFieldDefinition> FieldsOf(string? step) =>
        step is null || Steps.Count == 0 ? Fields : Fields.Where(f => string.Equals(f.StepName ?? Steps[0].Name, step, StringComparison.OrdinalIgnoreCase));
}

namespace CL.WebLogic.Forms.Models;

/// <summary>Validation errors by field name; the empty name holds form-level errors.</summary>
public sealed class WebFormErrors
{
    private readonly Dictionary<string, List<string>> _errors = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>True when there is at least one error.</summary>
    public bool Any => _errors.Count > 0;

    /// <summary>Adds an error (field "" = form-level).</summary>
    public void Add(string field, string message)
    {
        if (!_errors.TryGetValue(field ?? string.Empty, out var list))
            _errors[field ?? string.Empty] = list = [];
        if (!list.Contains(message))
            list.Add(message);
    }

    /// <summary>True when <paramref name="field"/> has an error.</summary>
    public bool Has(string field) => _errors.ContainsKey(field);

    /// <summary>Errors of one field.</summary>
    public IReadOnlyList<string> For(string field) => _errors.TryGetValue(field, out var list) ? list : [];

    /// <summary>Removes the errors of a field.</summary>
    public void Clear(string field) => _errors.Remove(field);

    /// <summary>Field errors (without form-level ones) for JSON.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Fields =>
        _errors.Where(e => e.Key.Length > 0).ToDictionary(e => e.Key, e => (IReadOnlyList<string>)e.Value, StringComparer.OrdinalIgnoreCase);

    /// <summary>Form-level errors.</summary>
    public IReadOnlyList<string> Form => For(string.Empty);
}

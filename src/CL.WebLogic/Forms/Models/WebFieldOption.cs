namespace CL.WebLogic.Forms.Models;

/// <summary>A choice of a select, radio, checkbox list or autocomplete field.</summary>
public sealed record WebFieldOption(string Value, string Label, bool Disabled = false, string? Group = null);

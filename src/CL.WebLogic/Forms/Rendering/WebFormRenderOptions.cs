namespace CL.WebLogic.Forms.Rendering;

/// <summary>Per-render overrides (from <c>{form:id action="…" class="…" submit="…" renderer="…"}</c>).</summary>
public sealed record WebFormRenderOptions
{
    /// <summary>Form action (local URL).</summary>
    public string? Action { get; init; }

    /// <summary>Extra CSS class.</summary>
    public string? CssClass { get; init; }

    /// <summary>Submit button text.</summary>
    public string? SubmitLabel { get; init; }

    /// <summary>Renderer name.</summary>
    public string? Renderer { get; init; }
}

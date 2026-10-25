namespace CL.WebLogic.Templates.Rendering;

/// <summary>An executable piece of a compiled template: writes into <see cref="TemplateRenderContext.Output"/>.</summary>
public delegate ValueTask TemplateBlock(TemplateRenderContext context);

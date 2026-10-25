namespace CL.WebLogic.Templates.Rendering;

/// <summary>A template failed to render (missing layout, unknown filter, recursion…).</summary>
public sealed class TemplateRenderException : Exception
{
    /// <summary>Creates the exception.</summary>
    public TemplateRenderException(string message, string? templatePath = null)
        : base(templatePath is null ? message : $"{templatePath}: {message}")
    {
        TemplatePath = templatePath;
    }

    /// <summary>Template that failed, when known.</summary>
    public string? TemplatePath { get; }
}

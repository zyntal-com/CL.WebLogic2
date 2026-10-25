namespace CL.WebLogic.Widgets.Models;

/// <summary>What a widget renders: trusted HTML, or a theme template with a model.</summary>
public sealed class WebWidgetResult
{
    private WebWidgetResult()
    {
    }

    /// <summary>Trusted, already-encoded HTML.</summary>
    public string? Html { get; private init; }

    /// <summary>Theme template (rendered without layout).</summary>
    public string? TemplatePath { get; private init; }

    /// <summary>Template model.</summary>
    public object? Model { get; private init; }

    /// <summary>Nothing (the widget hides itself).</summary>
    public static WebWidgetResult Empty { get; } = new() { Html = string.Empty };

    /// <summary>Trusted HTML — encode any untrusted values yourself, or use <see cref="Template"/>.</summary>
    public static WebWidgetResult FromHtml(string html) => new() { Html = html ?? string.Empty };

    /// <summary>A theme template such as <c>widgets/latest-posts</c>.</summary>
    public static WebWidgetResult Template(string templatePath, object? model = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(templatePath);
        return new WebWidgetResult { TemplatePath = templatePath, Model = model };
    }
}

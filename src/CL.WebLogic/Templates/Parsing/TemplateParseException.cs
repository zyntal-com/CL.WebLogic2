namespace CL.WebLogic.Templates.Parsing;

/// <summary>A template could not be parsed — or uses a value in an unsafe position.</summary>
public sealed class TemplateParseException : Exception
{
    /// <summary>Creates the exception.</summary>
    public TemplateParseException(string message, int line, int column, string? templatePath = null)
        : base(templatePath is null ? $"{message} (line {line}, column {column})" : $"{templatePath}({line},{column}): {message}")
    {
        Line = line;
        Column = column;
        TemplatePath = templatePath;
    }

    /// <summary>1-based line.</summary>
    public int Line { get; }

    /// <summary>1-based column.</summary>
    public int Column { get; }

    /// <summary>Template path, when known.</summary>
    public string? TemplatePath { get; }
}

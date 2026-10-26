namespace CL.WebLogic.Theming.Models;

/// <summary>A template in a theme that does not parse (see <c>WebThemeChecker</c>).</summary>
/// <param name="File">Path relative to the theme folder, with <c>/</c> separators.</param>
/// <param name="Line">1-based line.</param>
/// <param name="Column">1-based column.</param>
/// <param name="Message">What is wrong.</param>
public sealed record WebThemeIssue(string File, int Line, int Column, string Message)
{
    /// <inheritdoc/>
    public override string ToString() => $"{File}({Line},{Column}): {Message}";
}

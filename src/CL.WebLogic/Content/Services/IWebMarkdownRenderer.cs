using CL.WebLogic.Templates.Escaping;

namespace CL.WebLogic.Content.Services;

/// <summary>Renders Markdown to safe HTML (raw HTML in the input is encoded, links are scheme-checked).</summary>
public interface IWebMarkdownRenderer
{
    /// <summary>Renders <paramref name="markdown"/>.</summary>
    WebHtml Render(string? markdown);
}

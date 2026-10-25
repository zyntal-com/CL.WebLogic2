using CL.WebLogic.Templates.Escaping;

namespace CL.WebLogic.Content.Services;

/// <summary>Reduces untrusted HTML to an allowlisted subset of elements and attributes.</summary>
public interface IWebHtmlSanitizer
{
    /// <summary>Sanitizes <paramref name="html"/>.</summary>
    WebHtml Sanitize(string? html);
}

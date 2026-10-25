namespace CL.WebLogic.Templates.Escaping;

/// <summary>
/// Trusted HTML. A <see cref="WebHtml"/> value in the model is written unencoded in element
/// text; in any other context it is still encoded as text. Only wrap markup you produced or
/// sanitized (see <c>WebHtmlSanitizer</c>) — never raw user input.
/// </summary>
public sealed class WebHtml
{
    /// <summary>Wraps trusted markup.</summary>
    public WebHtml(string html) => Html = html ?? string.Empty;

    /// <summary>The markup.</summary>
    public string Html { get; }

    /// <summary>An empty fragment.</summary>
    public static WebHtml Empty { get; } = new(string.Empty);

    /// <inheritdoc/>
    public override string ToString() => Html;
}

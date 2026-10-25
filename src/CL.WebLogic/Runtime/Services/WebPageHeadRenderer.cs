using CL.WebLogic.Templates.Escaping;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using CL.WebLogic.Runtime.Models;

namespace CL.WebLogic.Runtime.Services;

/// <summary>Renders <see cref="WebPageMeta"/> into head markup for <c>{renderhead}</c>.</summary>
public static class WebPageHeadRenderer
{
    /// <summary>
    /// Renders the head block. <paramref name="fallbackTitle"/> / <paramref name="fallbackDescription"/>
    /// are used when the meta leaves them empty; <paramref name="nonce"/> is added to JSON-LD scripts.
    /// </summary>
    public static string Render(WebPageMeta? meta, string? fallbackTitle = null, string? fallbackDescription = null, string? nonce = null)
    {
        var m = meta ?? new WebPageMeta();
        var title = FirstNonEmpty(m.Title, fallbackTitle);
        var description = FirstNonEmpty(m.Description, fallbackDescription);
        var b = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(title))
            b.Append("<title>").Append(Encode(title)).Append("</title>\n");

        Name(b, "description", description);
        Name(b, "robots", m.Robots);
        Name(b, "keywords", m.Keywords.Count > 0 ? string.Join(", ", m.Keywords) : null);
        Link(b, "canonical", m.CanonicalUrl);

        foreach (var alternate in m.Alternates.Where(a => !string.IsNullOrWhiteSpace(a.Language) && !string.IsNullOrWhiteSpace(a.Url)))
            b.Append("<link data-wl-head rel=\"alternate\" hreflang=\"").Append(Encode(alternate.Language)).Append("\" href=\"").Append(Encode(alternate.Url)).Append("\">\n");

        Property(b, "og:title", FirstNonEmpty(m.OpenGraph?.Title, title));
        Property(b, "og:description", FirstNonEmpty(m.OpenGraph?.Description, description));
        Property(b, "og:type", m.OpenGraph?.Type);
        Property(b, "og:url", FirstNonEmpty(m.OpenGraph?.Url, m.CanonicalUrl));
        Property(b, "og:image", m.OpenGraph?.Image);
        Property(b, "og:image:alt", m.OpenGraph?.ImageAlt);
        Property(b, "og:site_name", m.OpenGraph?.SiteName);
        Property(b, "og:locale", FirstNonEmpty(m.OpenGraph?.Locale, m.Language));
        Property(b, "article:published_time", m.OpenGraph?.PublishedTime?.ToString("o", CultureInfo.InvariantCulture));
        Property(b, "article:modified_time", m.OpenGraph?.ModifiedTime?.ToString("o", CultureInfo.InvariantCulture));

        Name(b, "twitter:card", m.Twitter?.Card);
        Name(b, "twitter:title", FirstNonEmpty(m.Twitter?.Title, title));
        Name(b, "twitter:description", FirstNonEmpty(m.Twitter?.Description, description));
        Name(b, "twitter:image", FirstNonEmpty(m.Twitter?.Image, m.OpenGraph?.Image));
        Name(b, "twitter:site", m.Twitter?.Site);
        Name(b, "twitter:creator", m.Twitter?.Creator);

        foreach (var data in m.JsonLd)
        {
            b.Append("<script data-wl-head type=\"application/ld+json\"");
            if (!string.IsNullOrEmpty(nonce))
                b.Append(" nonce=\"").Append(Encode(nonce)).Append('"');
            // The default JavaScriptEncoder escapes <, >, & and quotes, so "</script>" cannot appear.
            b.Append('>').Append(JsonSerializer.Serialize(data, data.GetType(), WebJson.Default)).Append("</script>\n");
        }

        return b.ToString().TrimEnd('\n');
    }

    private static void Name(StringBuilder b, string name, string? content)
    {
        if (!string.IsNullOrWhiteSpace(content))
            b.Append("<meta data-wl-head name=\"").Append(name).Append("\" content=\"").Append(Encode(content)).Append("\">\n");
    }

    private static void Property(StringBuilder b, string property, string? content)
    {
        if (!string.IsNullOrWhiteSpace(content))
            b.Append("<meta data-wl-head property=\"").Append(property).Append("\" content=\"").Append(Encode(content)).Append("\">\n");
    }

    private static void Link(StringBuilder b, string rel, string? href)
    {
        if (!string.IsNullOrWhiteSpace(href))
            b.Append("<link data-wl-head rel=\"").Append(rel).Append("\" href=\"").Append(Encode(href)).Append("\">\n");
    }

    private static string Encode(string value) => TemplateEncoder.Html.Encode(value);

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}

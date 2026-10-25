using System.Text;

namespace CL.WebLogic.Results;

/// <summary>Formats <see cref="WebSseEvent"/>s in the <c>text/event-stream</c> wire format.</summary>
public static class WebSseWriter
{
    /// <summary>Formats one event, terminated by the blank line that dispatches it.</summary>
    public static string Format(WebSseEvent sseEvent)
    {
        ArgumentNullException.ThrowIfNull(sseEvent);
        var builder = new StringBuilder();

        // Field values must be single-line, otherwise a value could inject extra fields.
        if (!string.IsNullOrEmpty(sseEvent.Id))
            builder.Append("id: ").Append(SingleLine(sseEvent.Id)).Append('\n');
        if (!string.IsNullOrEmpty(sseEvent.Event))
            builder.Append("event: ").Append(SingleLine(sseEvent.Event)).Append('\n');
        if (sseEvent.RetryMs is { } retry and >= 0)
            builder.Append("retry: ").Append(retry).Append('\n');

        var data = (sseEvent.Data ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        foreach (var line in data.Split('\n'))
            builder.Append("data: ").Append(line).Append('\n');

        return builder.Append('\n').ToString();
    }

    /// <summary>A comment line, used as a keep-alive.</summary>
    public static string Comment(string text) => ": " + SingleLine(text) + "\n\n";

    private static string SingleLine(string value) =>
        value.Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", string.Empty, StringComparison.Ordinal);
}

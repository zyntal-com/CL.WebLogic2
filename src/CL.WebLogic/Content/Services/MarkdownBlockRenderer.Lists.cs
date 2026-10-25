using System.Text;

namespace CL.WebLogic.Content.Services;

// Lists and tables.
internal sealed partial class MarkdownBlockRenderer
{
    private readonly record struct ListMarker(bool Ordered, int Start, char Delimiter, int ContentOffset, int Indent);

    private int RenderList(IReadOnlyList<string> lines, int start, ListMarker first, StringBuilder output, int depth)
    {
        var items = new List<List<string>>();
        var loose = false;
        var i = start;
        var sawBlank = false;

        while (i < lines.Count)
        {
            var line = lines[i];
            if (TryListMarker(line, out var marker) && marker.Ordered == first.Ordered && marker.Delimiter == first.Delimiter && marker.Indent < first.ContentOffset)
            {
                if (sawBlank && items.Count > 0) loose = true;
                items.Add([line.Length > marker.ContentOffset ? line.Substring(marker.ContentOffset) : string.Empty]);
                sawBlank = false;
                i++;
                continue;
            }

            if (IsBlank(line))
            {
                // A blank line ends the list unless an indented continuation or another item follows.
                var next = i + 1;
                while (next < lines.Count && IsBlank(lines[next])) next++;
                if (next >= lines.Count)
                    break;
                var follows = Indent(lines[next]) >= first.ContentOffset
                    || (TryListMarker(lines[next], out var m) && m.Ordered == first.Ordered && m.Delimiter == first.Delimiter && m.Indent < first.ContentOffset);
                if (!follows)
                    break;
                items[^1].Add(string.Empty);
                sawBlank = true;
                i++;
                continue;
            }

            if (Indent(line) >= first.ContentOffset)
            {
                if (sawBlank) loose = true;
                items[^1].Add(StripIndent(line, first.ContentOffset));
                i++;
                continue;
            }

            if (!sawBlank && !InterruptsParagraph(line))
            {
                items[^1].Add(line.TrimStart()); // lazy paragraph continuation
                i++;
                continue;
            }

            break;
        }

        var tag = first.Ordered ? "ol" : "ul";
        output.Append('<').Append(tag);
        if (first.Ordered && first.Start != 1)
            output.Append(" start=\"").Append(first.Start).Append('"');
        output.Append(">\n");
        foreach (var item in items)
        {
            while (item.Count > 0 && IsBlank(item[^1])) item.RemoveAt(item.Count - 1);
            output.Append("<li>");
            var body = new StringBuilder();
            Render(item, body, depth + 1, tight: !loose);
            output.Append(body.ToString().TrimEnd('\n'));
            output.Append("</li>\n");
        }
        output.Append("</").Append(tag).Append(">\n");
        return i;
    }

    private static bool IsTableStart(IReadOnlyList<string> lines, int index) =>
        index + 1 < lines.Count && lines[index].Contains('|') && IsTableSeparator(lines[index + 1])
        && SplitRow(lines[index]).Count == SplitRow(lines[index + 1]).Count;

    private static bool IsTableSeparator(string line)
    {
        var cells = SplitRow(line);
        return cells.Count > 0 && cells.All(c =>
        {
            var t = c.Trim();
            return t.Length > 0 && t.Trim(':').Length > 0 && t.Trim(':').All(ch => ch == '-');
        });
    }

    private int RenderTable(IReadOnlyList<string> lines, int start, StringBuilder output)
    {
        var header = SplitRow(lines[start]);
        var alignments = SplitRow(lines[start + 1]).Select(c =>
        {
            var t = c.Trim();
            return (t.StartsWith(':'), t.EndsWith(':')) switch
            {
                (true, true) => "center",
                (false, true) => "right",
                (true, false) => "left",
                _ => null
            };
        }).ToList();

        output.Append("<table>\n<thead>\n<tr>");
        for (var c = 0; c < header.Count; c++)
            WriteCell(output, "th", header[c], alignments[c]);
        output.Append("</tr>\n</thead>\n");

        var i = start + 2;
        var wroteBody = false;
        while (i < lines.Count && !IsBlank(lines[i]) && lines[i].Contains('|'))
        {
            if (!wroteBody) { output.Append("<tbody>\n"); wroteBody = true; }
            var cells = SplitRow(lines[i]);
            output.Append("<tr>");
            for (var c = 0; c < header.Count; c++)
                WriteCell(output, "td", c < cells.Count ? cells[c] : string.Empty, alignments[c]);
            output.Append("</tr>\n");
            i++;
        }
        if (wroteBody) output.Append("</tbody>\n");
        output.Append("</table>\n");
        return i;
    }

    private void WriteCell(StringBuilder output, string tag, string content, string? alignment)
    {
        output.Append('<').Append(tag);
        if (alignment is not null)
            output.Append(" align=\"").Append(alignment).Append('"'); // not style=: strict CSP blocks inline styles
        output.Append('>');
        _inline.Render(content.Trim(), output, 0);
        output.Append("</").Append(tag).Append('>');
    }

    private static List<string> SplitRow(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.StartsWith('|')) trimmed = trimmed.Substring(1);
        if (trimmed.EndsWith('|') && !trimmed.EndsWith("\\|", StringComparison.Ordinal)) trimmed = trimmed.Substring(0, trimmed.Length - 1);

        var cells = new List<string>();
        var cell = new StringBuilder();
        for (var i = 0; i < trimmed.Length; i++)
        {
            if (trimmed[i] == '\\' && i + 1 < trimmed.Length && trimmed[i + 1] == '|') { cell.Append('|'); i++; }
            else if (trimmed[i] == '|') { cells.Add(cell.ToString()); cell.Clear(); }
            else cell.Append(trimmed[i]);
        }
        cells.Add(cell.ToString());
        return cells;
    }
}

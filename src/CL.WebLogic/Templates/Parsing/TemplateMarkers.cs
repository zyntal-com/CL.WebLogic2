namespace CL.WebLogic.Templates.Parsing;

/// <summary>Where a nested node list stops.</summary>
internal enum TemplateStopKind
{
    Document,
    Foreach,
    Section,
    IfBody,
    IfNotBody,
    SwitchBody,
    CaseBody,
    DefaultBody
}

/// <summary>Block markers (<c>{/if}</c>, <c>{else}</c>, <c>{case:…}</c>…) and which block owns them.</summary>
internal static class TemplateMarkers
{
    /// <summary>True for any block marker.</summary>
    public static bool IsMarker(string body) =>
        Eq(body, "/foreach") || Eq(body, "/if") || Eq(body, "/ifnot") || Eq(body, "/switch") ||
        Eq(body, "/section") || Eq(body, "/case") || Eq(body, "/default") ||
        Eq(body, "else") || Eq(body, "default") || Eq(body, "empty") ||
        StartsWith(body, "elseif:") || StartsWith(body, "case:");

    /// <summary>True when <paramref name="body"/> ends the current block.</summary>
    public static bool IsStop(TemplateStopKind stop, string body) => stop switch
    {
        TemplateStopKind.Foreach => Eq(body, "/foreach") || Eq(body, "empty"),
        TemplateStopKind.Section => Eq(body, "/section"),
        TemplateStopKind.IfBody => Eq(body, "/if") || Eq(body, "else") || StartsWith(body, "elseif:"),
        TemplateStopKind.IfNotBody => Eq(body, "/ifnot") || Eq(body, "else") || StartsWith(body, "elseif:"),
        TemplateStopKind.SwitchBody => Eq(body, "/switch") || Eq(body, "default") || StartsWith(body, "case:"),
        TemplateStopKind.CaseBody or TemplateStopKind.DefaultBody =>
            Eq(body, "/case") || Eq(body, "/default") || Eq(body, "/switch") || Eq(body, "default") || StartsWith(body, "case:"),
        _ => false
    };

    /// <summary>The closing marker a block must end with (for error messages).</summary>
    public static string Closer(TemplateStopKind stop) => stop switch
    {
        TemplateStopKind.Foreach => "{/foreach}",
        TemplateStopKind.Section => "{/section}",
        TemplateStopKind.IfBody => "{/if}",
        TemplateStopKind.IfNotBody => "{/ifnot}",
        _ => "{/switch}"
    };

    /// <summary>Case-insensitive equality.</summary>
    public static bool Eq(string body, string keyword) => string.Equals(body, keyword, StringComparison.OrdinalIgnoreCase);

    /// <summary>Case-insensitive prefix test.</summary>
    public static bool StartsWith(string body, string prefix) =>
        body.Length >= prefix.Length && string.Compare(body, 0, prefix, 0, prefix.Length, StringComparison.OrdinalIgnoreCase) == 0;
}

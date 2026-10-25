using CL.WebLogic.Templates.Ast;

namespace CL.WebLogic.Templates.Parsing;

/// <summary>
/// The small expression grammars inside tokens and conditions: selectors (<c>scope:path</c>),
/// filter chains (<c>|name:arg</c>) and <c>{if:…}</c> conditions.
/// </summary>
internal static class SelectorSyntax
{
    /// <summary>Maps a scope word to a scope, or null when it is not a known scope.</summary>
    public static TemplateScope? MapScope(string scopeWord) => scopeWord.Trim().ToLowerInvariant() switch
    {
        "model" => TemplateScope.Model,
        "page" => TemplateScope.Page,
        "query" => TemplateScope.Query,
        "route" => TemplateScope.Route,
        "item" => TemplateScope.Item,
        "param" => TemplateScope.Param,
        "loop" => TemplateScope.Loop,
        "user" => TemplateScope.User,
        _ => null
    };

    /// <summary>
    /// Parses <c>scope:path</c> or a bare <c>path</c>. A scope word that names an active loop
    /// alias becomes an alias selector; any other unknown scope word returns null.
    /// </summary>
    public static TemplateSelector? TryParseSelector(string raw, Func<string, bool> isAlias)
    {
        var selector = (raw ?? string.Empty).Trim();
        var separator = selector.IndexOf(':');
        if (separator < 0)
            return new TemplateSelector(TemplateScope.None, selector);

        var word = selector.Substring(0, separator).Trim();
        var key = selector.Substring(separator + 1).Trim();

        if (MapScope(word) is { } scope)
            return new TemplateSelector(scope, key);

        return isAlias(word) ? new TemplateSelector(TemplateScope.Alias, key, word) : null;
    }

    /// <summary>Splits <c>selector|filter:arg|filter</c>; pipes inside quotes are ignored.</summary>
    public static (string Selector, IReadOnlyList<TemplateFilter> Filters) ParseExpression(string expression)
    {
        var pipe = expression.IndexOf('|');
        if (pipe < 0)
            return (expression.Trim(), []);

        var filters = new List<TemplateFilter>();
        foreach (var raw in SplitFilterChain(expression.Substring(pipe + 1)))
        {
            var trimmed = raw.Trim();
            if (trimmed.Length == 0)
                continue;

            var colon = trimmed.IndexOf(':');
            filters.Add(colon < 0
                ? new TemplateFilter(trimmed, null)
                : new TemplateFilter(trimmed.Substring(0, colon).Trim(), Unquote(trimmed.Substring(colon + 1).Trim())));
        }

        return (expression.Substring(0, pipe).Trim(), filters);
    }

    /// <summary>
    /// Parses a condition: <c>auth</c>, <c>group:a,b</c> (or <c>accessgroup:a,b</c>), <c>permission:x</c>, a comparison
    /// (<c>lhs op rhs</c>) or plain selector truthiness. Returns null for an unknown scope.
    /// </summary>
    public static TemplateCondition? TryParseCondition(string expression, Func<string, bool> isAlias)
    {
        var expr = (expression ?? string.Empty).Trim();

        if (string.Equals(expr, "auth", StringComparison.OrdinalIgnoreCase))
            return new AuthCondition();

        if (expr.StartsWith("group:", StringComparison.OrdinalIgnoreCase))
            return new GroupCondition(SplitValues(expr.Substring("group:".Length)));

        // v1 spelling, kept so existing themes parse unchanged.
        if (expr.StartsWith("accessgroup:", StringComparison.OrdinalIgnoreCase))
            return new GroupCondition(SplitValues(expr.Substring("accessgroup:".Length)));

        if (expr.StartsWith("permission:", StringComparison.OrdinalIgnoreCase))
            return new PermissionCondition(expr.Substring("permission:".Length).Trim());

        var parts = expr.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 3 && TryParseOp(parts[1], out var op))
        {
            var left = TryParseSelector(parts[0], isAlias);
            return left is null ? null : new CompareCondition(left, op, ParseOperand(parts[2], isAlias));
        }

        var selector = TryParseSelector(expr, isAlias);
        return selector is null ? null : new TruthyCondition(selector);
    }

    /// <summary>Splits a comma / semicolon list.</summary>
    public static IReadOnlyList<string> SplitValues(string value) =>
        value.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static TemplateOperand ParseOperand(string raw, Func<string, bool> isAlias)
    {
        var token = raw.Trim();
        if (token.Length >= 2 && (token[0] == '"' || token[0] == '\'') && token[^1] == token[0])
            return TemplateOperand.FromLiteral(token.Substring(1, token.Length - 2));

        var separator = token.IndexOf(':');
        if (separator > 0)
        {
            var word = token.Substring(0, separator);
            if ((MapScope(word) is not null || isAlias(word)) && TryParseSelector(token, isAlias) is { } selector)
                return TemplateOperand.FromSelector(selector);
        }

        return TemplateOperand.FromLiteral(token);
    }

    private static bool TryParseOp(string word, out CompareOp op)
    {
        switch (word.Trim().ToLowerInvariant())
        {
            case "eq": op = CompareOp.Eq; return true;
            case "ne": op = CompareOp.Ne; return true;
            case "gt": op = CompareOp.Gt; return true;
            case "gte": op = CompareOp.Gte; return true;
            case "lt": op = CompareOp.Lt; return true;
            case "lte": op = CompareOp.Lte; return true;
            default: op = CompareOp.Eq; return false;
        }
    }

    private static string Unquote(string value) =>
        value.Length >= 2 && (value[0] == '"' || value[0] == '\'') && value[^1] == value[0]
            ? value.Substring(1, value.Length - 2)
            : value;

    private static IEnumerable<string> SplitFilterChain(string input)
    {
        var quote = '\0';
        var start = 0;
        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (quote == '\0' && (c == '"' || c == '\''))
                quote = c;
            else if (c == quote)
                quote = '\0';
            else if (c == '|' && quote == '\0')
            {
                yield return input.Substring(start, i - start);
                start = i + 1;
            }
        }

        if (start < input.Length)
            yield return input.Substring(start);
    }
}

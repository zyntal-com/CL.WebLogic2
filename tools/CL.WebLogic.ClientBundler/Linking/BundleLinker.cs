using System.Text;
using CL.WebLogic.ClientBundler.Lexing;

namespace CL.WebLogic.ClientBundler.Linking;

/// <summary>
/// Links an entry module and everything it imports into one script. Each module becomes a
/// function in a private registry, evaluated once in dependency order; imports read the
/// dependency's frozen export object. Cycles and missing exports are errors.
/// </summary>
internal sealed class BundleLinker
{
    private readonly Func<string, string?> _readSource;
    private readonly Dictionary<string, JsModule> _modules = new(StringComparer.Ordinal);
    private readonly List<string> _order = [];
    private readonly List<string> _visiting = [];

    /// <summary>Creates a linker that reads module sources (by root-relative path) with <paramref name="readSource"/>.</summary>
    public BundleLinker(Func<string, string?> readSource) => _readSource = readSource;

    /// <summary>Modules in evaluation order after <see cref="Link"/>.</summary>
    public IReadOnlyList<string> Order => _order;

    /// <summary>Links <paramref name="entry"/> into a bundle.</summary>
    public string Link(string entry, BundleFormat format, string header)
    {
        Visit(entry, entry, 0);
        foreach (var module in _modules.Values)
        {
            foreach (var import in module.Imports)
            {
                var target = _modules[import.ResolvedPath];
                foreach (var (imported, _) in import.Names)
                {
                    if (!target.Exports.ContainsKey(imported))
                        throw new BundleException($"'{imported}' is not exported by '{import.ResolvedPath}'", module.Path, import.Line);
                }
            }
        }

        var output = new StringBuilder();
        if (header.Length > 0)
            output.Append("/*! ").Append(header.Replace("*/", "* /", StringComparison.Ordinal)).Append(" */\n");
        if (format == BundleFormat.Iife)
            output.Append("(function () {\n\"use strict\";\n");
        output.Append("const __wl_m = Object.create(null);\n");

        foreach (var path in _order)
        {
            var module = _modules[path];
            output.Append("// ").Append(path).Append('\n');
            output.Append("__wl_m[\"").Append(path).Append("\"] = (function () {\n");
            output.Append(Compact(module.Body));
            output.Append("\nreturn Object.freeze({ ");
            output.AppendJoin(", ", module.Exports.Select(e => e.Key == e.Value ? e.Key : e.Key + ": " + e.Value));
            output.Append(" });\n})();\n");
        }

        var entryExports = _modules[entry].Exports.Keys.ToList();
        if (format == BundleFormat.Iife)
            output.Append("})();\n");
        else if (entryExports.Count > 0)
            output.Append("export const { ").AppendJoin(", ", entryExports).Append(" } = __wl_m[\"").Append(entry).Append("\"];\n");
        return output.ToString();
    }

    /// <summary>Drops comments and collapses whitespace outside literals (line breaks are kept for ASI).</summary>
    public static string Compact(IEnumerable<JsToken> tokens)
    {
        var builder = new StringBuilder();
        var pending = 0; // 0 none, 1 space, 2 newline
        foreach (var token in tokens)
        {
            switch (token.Kind)
            {
                case JsTokenKind.LineComment:
                    continue;
                case JsTokenKind.Whitespace:
                case JsTokenKind.BlockComment:
                    pending = Math.Max(pending, token.Text.Contains('\n', StringComparison.Ordinal) ? 2 : 1);
                    continue;
            }
            if (token.Text.Length == 0)
                continue;
            if (pending == 2 && builder.Length > 0)
                builder.Append('\n');
            else if (pending == 1 && builder.Length > 0 && builder[^1] != '\n')
                builder.Append(' ');
            pending = 0;
            builder.Append(token.Text);
        }
        return builder.ToString();
    }

    private void Visit(string path, string from, int line)
    {
        if (_modules.ContainsKey(path))
            return;
        if (_visiting.Contains(path))
            throw new BundleException("import cycle: " + string.Join(" → ", _visiting.SkipWhile(p => p != path).Append(path)), from, line);

        var source = _readSource(path) ?? throw new BundleException($"module '{path}' not found", from, line);
        var module = ModuleParser.Parse(path, source);
        _visiting.Add(path);
        foreach (var import in module.Imports)
            Visit(import.ResolvedPath, path, import.Line);
        _visiting.RemoveAt(_visiting.Count - 1);
        _modules[path] = module;
        _order.Add(path);
    }
}

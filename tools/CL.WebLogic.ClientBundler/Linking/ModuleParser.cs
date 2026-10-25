using CL.WebLogic.ClientBundler.Lexing;

namespace CL.WebLogic.ClientBundler.Linking;

/// <summary>
/// Parses the supported module subset:
/// <c>import { a, b as c } from './x.js'</c>, <c>import * as ns from './x.js'</c>,
/// <c>import './x.js'</c>, <c>export function|async function|class|const name</c> and
/// <c>export { a, b as c }</c>. Everything else that touches the module system (default
/// exports, <c>export let/var</c>, re-exports, dynamic <c>import()</c>, <c>import.meta</c>,
/// bare specifiers) is a <see cref="BundleException"/>.
/// </summary>
internal static class ModuleParser
{
    /// <summary>Parses <paramref name="source"/> as the module at <paramref name="path"/> (root-relative, forward slashes).</summary>
    public static JsModule Parse(string path, string source)
    {
        var tokens = JsLexer.Tokenize(source, path);
        var body = new List<JsToken>(tokens.Count);
        var imports = new List<ModuleImport>();
        var exports = new Dictionary<string, string>(StringComparer.Ordinal);
        var depth = 0;
        JsToken? previous = null;

        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token.IsTrivia)
            {
                body.Add(token);
                continue;
            }

            var afterDot = previous is { } p && (p.Is(".") || p.Is("?."));
            if (depth == 0 && !afterDot && token.IsWord("import"))
            {
                var (import, end) = ParseImport(tokens, i, path);
                if (import is not null)
                {
                    imports.Add(import);
                    body.Add(new JsToken(JsTokenKind.Identifier, Replacement(import), token.Line));
                }
                i = end;
                previous = tokens[end];
                continue;
            }

            if (depth == 0 && !afterDot && token.IsWord("export"))
            {
                // Declarations: only the keyword is dropped. Export lists: the whole statement is.
                i = ParseExport(tokens, i, path, exports);
                previous = tokens[i];
                continue;
            }

            if (token.Is("{") || token.Is("(") || token.Is("["))
                depth++;
            else if (token.Is("}") || token.Is(")") || token.Is("]"))
                depth--;
            body.Add(token);
            previous = token;
        }

        return new JsModule { Path = path, Body = body, Imports = imports, Exports = exports };
    }

    private static (ModuleImport? Import, int End) ParseImport(List<JsToken> tokens, int start, string path)
    {
        var line = tokens[start].Line;
        var i = NextSignificant(tokens, start, path);
        var first = tokens[i];
        if (first.Is("(") || first.Is("."))
            throw new BundleException("dynamic import() and import.meta are not supported", path, line);

        if (first.Kind == JsTokenKind.String)
        {
            var end = SkipSemicolon(tokens, i);
            return (new ModuleImport(Unquote(first, path), Resolve(path, Unquote(first, path), line), [], null, line), end);
        }

        var names = new List<(string, string)>();
        string? ns = null;
        if (first.Is("{"))
        {
            i = NextSignificant(tokens, i, path);
            while (!tokens[i].Is("}"))
            {
                var imported = Expect(tokens[i], JsTokenKind.Identifier, path, "an imported name");
                var local = imported;
                i = NextSignificant(tokens, i, path);
                if (tokens[i].IsWord("as"))
                {
                    i = NextSignificant(tokens, i, path);
                    local = Expect(tokens[i], JsTokenKind.Identifier, path, "a local name");
                    i = NextSignificant(tokens, i, path);
                }
                if (imported == "default")
                    throw new BundleException("default imports are not supported; use named exports", path, line);
                names.Add((imported, local));
                if (tokens[i].Is(","))
                    i = NextSignificant(tokens, i, path);
                else if (!tokens[i].Is("}"))
                    throw new BundleException("expected ',' or '}' in import list", path, tokens[i].Line);
            }
        }
        else if (first.Is("*"))
        {
            i = NextSignificant(tokens, i, path);
            if (!tokens[i].IsWord("as"))
                throw new BundleException("expected 'as' after 'import *'", path, line);
            i = NextSignificant(tokens, i, path);
            ns = Expect(tokens[i], JsTokenKind.Identifier, path, "a namespace name");
        }
        else
        {
            throw new BundleException("default imports are not supported; use import { name } from '...'", path, line);
        }

        i = NextSignificant(tokens, i, path);
        if (!tokens[i].IsWord("from"))
            throw new BundleException("expected 'from'", path, tokens[i].Line);
        i = NextSignificant(tokens, i, path);
        if (tokens[i].Kind != JsTokenKind.String)
            throw new BundleException("expected a module specifier string", path, tokens[i].Line);
        var specifier = Unquote(tokens[i], path);
        return (new ModuleImport(specifier, Resolve(path, specifier, line), names, ns, line), SkipSemicolon(tokens, i));
    }

    private static int ParseExport(List<JsToken> tokens, int start, string path, Dictionary<string, string> exports)
    {
        var line = tokens[start].Line;
        var i = NextSignificant(tokens, start, path);
        var next = tokens[i];

        if (next.IsWord("function") || next.IsWord("class") || next.IsWord("const") || next.IsWord("async"))
        {
            var j = i;
            if (tokens[j].IsWord("async"))
            {
                j = NextSignificant(tokens, j, path);
                if (!tokens[j].IsWord("function"))
                    throw new BundleException("expected 'function' after 'export async'", path, line);
            }
            var keyword = tokens[j].Text;
            j = NextSignificant(tokens, j, path);
            if (keyword == "function" && tokens[j].Is("*"))
                j = NextSignificant(tokens, j, path);
            if (keyword == "const" && (tokens[j].Is("{") || tokens[j].Is("[")))
                throw new BundleException("destructuring exports are not supported", path, line);
            var name = Expect(tokens[j], JsTokenKind.Identifier, path, "a declaration name");
            Add(exports, name, name, path, line);
            return start; // caller drops just the 'export' keyword
        }

        if (next.Is("{"))
        {
            i = NextSignificant(tokens, i, path);
            while (!tokens[i].Is("}"))
            {
                var local = Expect(tokens[i], JsTokenKind.Identifier, path, "an exported name");
                var exported = local;
                i = NextSignificant(tokens, i, path);
                if (tokens[i].IsWord("as"))
                {
                    i = NextSignificant(tokens, i, path);
                    exported = Expect(tokens[i], JsTokenKind.Identifier, path, "an export alias");
                    i = NextSignificant(tokens, i, path);
                }
                Add(exports, exported, local, path, line);
                if (tokens[i].Is(","))
                    i = NextSignificant(tokens, i, path);
                else if (!tokens[i].Is("}"))
                    throw new BundleException("expected ',' or '}' in export list", path, tokens[i].Line);
            }
            var after = PeekSignificant(tokens, i);
            if (after is not null && tokens[after.Value].IsWord("from"))
                throw new BundleException("re-exports (export … from) are not supported", path, line);
            return SkipSemicolon(tokens, i);
        }

        var reason = next.Text switch
        {
            "default" => "default exports are not supported; use named exports",
            "let" or "var" => "export let/var is not supported (exports are snapshots); use const or a function",
            "*" => "export * is not supported",
            _ => "unsupported export form"
        };
        throw new BundleException(reason, path, line);
    }

    private static void Add(Dictionary<string, string> exports, string exported, string local, string path, int line)
    {
        if (exported == "default")
            throw new BundleException("default exports are not supported; use named exports", path, line);
        if (!exports.TryAdd(exported, local))
            throw new BundleException($"'{exported}' is exported twice", path, line);
    }

    private static string Replacement(ModuleImport import)
    {
        var key = "__wl_m[\"" + import.ResolvedPath + "\"]";
        if (import.Namespace is not null)
            return "const " + import.Namespace + " = " + key + ";";
        if (import.Names.Count == 0)
            return string.Empty;
        return "const { " + string.Join(", ", import.Names.Select(n => n.Imported == n.Local ? n.Local : n.Imported + ": " + n.Local)) + " } = " + key + ";";
    }

    /// <summary>Resolves a relative specifier against the importing module; must stay inside the source root.</summary>
    public static string Resolve(string fromPath, string specifier, int line)
    {
        if (!(specifier.StartsWith("./", StringComparison.Ordinal) || specifier.StartsWith("../", StringComparison.Ordinal)) || !specifier.EndsWith(".js", StringComparison.Ordinal))
            throw new BundleException($"'{specifier}': only relative './…/name.js' specifiers are supported", fromPath, line);

        var segments = new List<string>(fromPath.Split('/')[..^1]);
        foreach (var part in specifier.Split('/'))
        {
            if (part == ".") continue;
            if (part == "..")
            {
                if (segments.Count == 0)
                    throw new BundleException($"'{specifier}' points outside the client source folder", fromPath, line);
                segments.RemoveAt(segments.Count - 1);
                continue;
            }
            segments.Add(part);
        }
        return string.Join('/', segments);
    }

    private static string Unquote(JsToken token, string path)
    {
        var value = token.Text[1..^1];
        if (value.Contains('\\', StringComparison.Ordinal))
            throw new BundleException("escapes are not allowed in module specifiers", path, token.Line);
        return value;
    }

    private static string Expect(JsToken token, JsTokenKind kind, string path, string what) =>
        token.Kind == kind ? token.Text : throw new BundleException($"expected {what}, found '{token.Text}'", path, token.Line);

    private static int NextSignificant(List<JsToken> tokens, int index, string path) =>
        PeekSignificant(tokens, index) ?? throw new BundleException("unexpected end of file", path, tokens[index].Line);

    private static int? PeekSignificant(List<JsToken> tokens, int index)
    {
        for (var i = index + 1; i < tokens.Count; i++)
        {
            if (!tokens[i].IsTrivia)
                return i;
        }
        return null;
    }

    private static int SkipSemicolon(List<JsToken> tokens, int index) =>
        PeekSignificant(tokens, index) is { } next && tokens[next].Is(";") ? next : index;
}

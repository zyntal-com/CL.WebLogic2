using System.Text;
using CL.WebLogic.ClientBundler.Linking;

namespace CL.WebLogic.ClientBundler.Lexing;

/// <summary>
/// A JavaScript lexer that is exact about the parts that matter for bundling: comments, strings,
/// template literals (with nested expressions) and regular-expression literals versus division.
/// A <c>/</c> starts a regular expression unless it follows an identifier, a literal, <c>)</c>,
/// <c>]</c> or <c>}</c> (keywords like <c>return</c> excepted).
/// </summary>
internal sealed class JsLexer
{
    private static readonly string[] Punctuators =
    [
        ">>>=", "...", "===", "!==", "**=", "<<=", ">>=", ">>>", "&&=", "||=", "??=",
        "=>", "==", "!=", "<=", ">=", "&&", "||", "??", "?.", "++", "--", "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^=", "**", "<<", ">>"
    ];

    private static readonly HashSet<string> RegexAfterWords = new(StringComparer.Ordinal)
    {
        "return", "typeof", "instanceof", "in", "of", "new", "delete", "void", "throw", "case", "do", "else", "yield", "await"
    };

    private readonly string _source;
    private readonly string _file;
    private int _pos;
    private int _line = 1;

    private JsLexer(string source, string file)
    {
        _source = source;
        _file = file;
    }

    /// <summary>Tokenizes <paramref name="source"/>; throws <see cref="BundleException"/> on unterminated literals.</summary>
    public static List<JsToken> Tokenize(string source, string file = "<input>")
    {
        var lexer = new JsLexer(source.Replace("\r\n", "\n", StringComparison.Ordinal), file);
        var tokens = new List<JsToken>();
        lexer.ReadUntil(tokens, stopAtBrace: false);
        return tokens;
    }

    private void ReadUntil(List<JsToken> tokens, bool stopAtBrace)
    {
        var depth = 0;
        JsToken? previous = null;
        while (_pos < _source.Length)
        {
            var c = _source[_pos];
            if (stopAtBrace && c == '}' && depth == 0)
                return;

            var token = Next(c, previous);
            tokens.Add(token);
            if (!token.IsTrivia)
                previous = token;
            if (token.Is("{")) depth++;
            else if (token.Is("}")) depth--;
        }
        if (stopAtBrace)
            throw Error("unterminated template expression");
    }

    private JsToken Next(char c, JsToken? previous)
    {
        var line = _line;
        if (char.IsWhiteSpace(c))
            return Make(JsTokenKind.Whitespace, ReadWhile(char.IsWhiteSpace), line);
        if (c == '/' && Peek(1) == '/')
            return Make(JsTokenKind.LineComment, ReadWhile(ch => ch != '\n'), line);
        if (c == '/' && Peek(1) == '*')
            return Make(JsTokenKind.BlockComment, ReadBlockComment(), line);
        if (c is '"' or '\'')
            return Make(JsTokenKind.String, ReadString(c), line);
        if (c == '`')
            return Make(JsTokenKind.Template, ReadTemplate(), line);
        if (char.IsAsciiDigit(c) || (c == '.' && char.IsAsciiDigit(Peek(1))))
            return Make(JsTokenKind.Number, ReadNumber(), line);
        if (IsIdentifierStart(c) || (c == '#' && IsIdentifierStart(Peek(1))))
        {
            var start = _pos++;
            while (_pos < _source.Length && IsIdentifierPart(_source[_pos]))
                _pos++;
            return Make(JsTokenKind.Identifier, _source[start.._pos], line);
        }
        if (c == '/' && RegexAllowed(previous))
            return Make(JsTokenKind.RegExp, ReadRegex(), line);

        foreach (var p in Punctuators)
        {
            if (string.CompareOrdinal(_source, _pos, p, 0, p.Length) == 0)
            {
                _pos += p.Length;
                return Make(JsTokenKind.Punctuator, p, line);
            }
        }
        _pos++;
        return Make(JsTokenKind.Punctuator, c.ToString(), line);
    }

    private static bool RegexAllowed(JsToken? previous) => previous switch
    {
        null => true,
        { Kind: JsTokenKind.Identifier } p => RegexAfterWords.Contains(p.Text),
        { Kind: JsTokenKind.Number or JsTokenKind.String or JsTokenKind.Template or JsTokenKind.RegExp } => false,
        { Kind: JsTokenKind.Punctuator } p => p.Text is not (")" or "]" or "}"),
        _ => true
    };

    private string ReadBlockComment()
    {
        var end = _source.IndexOf("*/", _pos + 2, StringComparison.Ordinal);
        if (end < 0)
            throw Error("unterminated comment");
        var text = _source[_pos..(end + 2)];
        _pos = end + 2;
        _line += text.Count(ch => ch == '\n');
        return text;
    }

    private string ReadString(char quote)
    {
        var start = _pos++;
        while (_pos < _source.Length)
        {
            var c = _source[_pos++];
            if (c == '\\') { _pos++; continue; }
            if (c == quote) return _source[start.._pos];
            if (c == '\n') break;
        }
        throw Error("unterminated string");
    }

    private string ReadTemplate()
    {
        var builder = new StringBuilder();
        builder.Append(_source[_pos++]);
        while (_pos < _source.Length)
        {
            var c = _source[_pos];
            if (c == '\\')
            {
                builder.Append(_source, _pos, Math.Min(2, _source.Length - _pos));
                _pos += 2;
                continue;
            }
            if (c == '`')
            {
                builder.Append(c);
                _pos++;
                return builder.ToString();
            }
            if (c == '$' && Peek(1) == '{')
            {
                builder.Append("${");
                _pos += 2;
                var inner = new List<JsToken>();
                ReadUntil(inner, stopAtBrace: true);
                foreach (var token in inner)
                    builder.Append(token.Text);
                builder.Append('}');
                _pos++; // the closing brace
                continue;
            }
            if (c == '\n') _line++;
            builder.Append(c);
            _pos++;
        }
        throw Error("unterminated template literal");
    }

    private string ReadRegex()
    {
        var start = _pos++;
        var inClass = false;
        while (_pos < _source.Length)
        {
            var c = _source[_pos++];
            if (c == '\\') { _pos++; continue; }
            if (c == '\n') break;
            if (c == '[') inClass = true;
            else if (c == ']') inClass = false;
            else if (c == '/' && !inClass)
            {
                while (_pos < _source.Length && char.IsAsciiLetter(_source[_pos]))
                    _pos++;
                return _source[start.._pos];
            }
        }
        throw Error("unterminated regular expression");
    }

    private string ReadNumber()
    {
        var start = _pos;
        while (_pos < _source.Length)
        {
            var c = _source[_pos];
            if (char.IsAsciiLetterOrDigit(c) || c is '_' or '.')
            {
                _pos++;
                if (c is 'e' or 'E' && Peek(0) is '+' or '-' && !_source[start..].StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    _pos++;
                continue;
            }
            break;
        }
        return _source[start.._pos];
    }

    private string ReadWhile(Func<char, bool> predicate)
    {
        var start = _pos;
        while (_pos < _source.Length && predicate(_source[_pos]))
        {
            if (_source[_pos] == '\n') _line++;
            _pos++;
        }
        return _source[start.._pos];
    }

    private char Peek(int offset) => _pos + offset < _source.Length ? _source[_pos + offset] : '\0';

    private static JsToken Make(JsTokenKind kind, string text, int line) => new(kind, text, line);

    private static bool IsIdentifierStart(char c) => char.IsLetter(c) || c is '_' or '$';

    private static bool IsIdentifierPart(char c) => char.IsLetterOrDigit(c) || c is '_' or '$';

    private BundleException Error(string message) => new(message, _file, _line);
}

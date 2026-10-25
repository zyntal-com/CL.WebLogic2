namespace CL.WebLogic.ClientBundler.Lexing;

/// <summary>Token kinds of the JavaScript lexer.</summary>
internal enum JsTokenKind
{
    /// <summary>Spaces, tabs and line breaks.</summary>
    Whitespace,

    /// <summary><c>// …</c></summary>
    LineComment,

    /// <summary><c>/* … */</c></summary>
    BlockComment,

    /// <summary>Identifier or keyword (including <c>#private</c> names).</summary>
    Identifier,

    /// <summary>Numeric literal.</summary>
    Number,

    /// <summary>Single- or double-quoted string.</summary>
    String,

    /// <summary>A whole template literal, including its <c>${…}</c> expressions.</summary>
    Template,

    /// <summary>Regular expression literal.</summary>
    RegExp,

    /// <summary>Operator or punctuation.</summary>
    Punctuator
}

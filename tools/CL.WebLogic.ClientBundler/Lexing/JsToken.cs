namespace CL.WebLogic.ClientBundler.Lexing;

/// <summary>A token with its source text and 1-based line.</summary>
internal readonly record struct JsToken(JsTokenKind Kind, string Text, int Line)
{
    /// <summary>True for whitespace and comments.</summary>
    public bool IsTrivia => Kind is JsTokenKind.Whitespace or JsTokenKind.LineComment or JsTokenKind.BlockComment;

    /// <summary>True when this is the punctuator <paramref name="value"/>.</summary>
    public bool Is(string value) => Kind == JsTokenKind.Punctuator && Text == value;

    /// <summary>True when this is the identifier/keyword <paramref name="value"/>.</summary>
    public bool IsWord(string value) => Kind == JsTokenKind.Identifier && Text == value;
}

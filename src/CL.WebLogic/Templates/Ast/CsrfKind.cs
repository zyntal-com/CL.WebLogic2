namespace CL.WebLogic.Templates.Ast;

/// <summary>Which CSRF markup a <see cref="CsrfNode"/> emits.</summary>
public enum CsrfKind
{
    /// <summary><c>{csrf}</c> — a hidden form input.</summary>
    Field,

    /// <summary><c>{csrf_token}</c> — the token value.</summary>
    Token,

    /// <summary><c>{csrf_meta}</c> — a <c>&lt;meta name="csrf-token"&gt;</c> tag.</summary>
    Meta
}

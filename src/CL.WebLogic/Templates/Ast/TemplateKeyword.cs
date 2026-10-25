namespace CL.WebLogic.Templates.Ast;

/// <summary>Argument-less directives that expand to engine-provided markup.</summary>
public enum TemplateKeyword
{
    /// <summary><c>{renderbody}</c> — the child template's body inside a layout.</summary>
    RenderBody,

    /// <summary><c>{renderhead}</c> — title, meta, canonical, Open Graph and JSON-LD.</summary>
    RenderHead,

    /// <summary><c>{nonce}</c> — the per-request CSP nonce.</summary>
    Nonce,

    /// <summary><c>{weblogic_scripts}</c> — the WebLogic client script tags (with nonce).</summary>
    WebLogicScripts,

    /// <summary>
    /// Inserted by the parser into static <c>&lt;script&gt;</c> / <c>&lt;style&gt;</c> tags that have
    /// no nonce: writes <c> nonce="…"</c>, or nothing outside a request.
    /// </summary>
    NonceAttribute
}

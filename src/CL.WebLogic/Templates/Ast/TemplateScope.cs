namespace CL.WebLogic.Templates.Ast;

/// <summary>Where a selector reads its value from.</summary>
public enum TemplateScope
{
    /// <summary>No scope: a top-level model key.</summary>
    None,

    /// <summary><c>model:</c> — the page model (dotted paths walk dictionaries and properties).</summary>
    Model,

    /// <summary><c>page:</c> — request facts (path, method, query_string, is_https…).</summary>
    Page,

    /// <summary><c>query:</c> — a query-string parameter.</summary>
    Query,

    /// <summary><c>route:</c> — a route value of the matched endpoint.</summary>
    Route,

    /// <summary><c>item:</c> — the current <c>{foreach}</c> item.</summary>
    Item,

    /// <summary><c>param:</c> — a parameter passed to the partial.</summary>
    Param,

    /// <summary><c>loop:</c> — loop metadata of the enclosing <c>{foreach}</c>.</summary>
    Loop,

    /// <summary><c>user:</c> — the signed-in user (id, name, is_authenticated).</summary>
    User,

    /// <summary>A <c>{foreach … as alias}</c> name (see <see cref="TemplateSelector.Alias"/>).</summary>
    Alias
}

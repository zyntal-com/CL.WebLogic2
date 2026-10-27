using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Templates.Rendering;

/// <summary>Produces the markup for <c>{weblogic_scripts}</c> (the client runtime tags).</summary>
public interface ITemplateScriptProvider
{
    /// <summary>Script / link tags, carrying <paramref name="nonce"/>.</summary>
    string GetScriptTags(HttpContext? httpContext, string nonce);

    /// <summary>
    /// Script / link tags for a document rendered by <paramref name="layout"/> (the outermost template, or null
    /// for fragments). WebLogic's client uses it to tell layouts apart during partial navigation.
    /// </summary>
    string GetScriptTags(HttpContext? httpContext, string nonce, string? layout) => GetScriptTags(httpContext, nonce);
}

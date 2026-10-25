using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Templates.Rendering;

/// <summary>Produces the markup for <c>{weblogic_scripts}</c> (the client runtime tags).</summary>
public interface ITemplateScriptProvider
{
    /// <summary>Script / link tags, carrying <paramref name="nonce"/>.</summary>
    string GetScriptTags(HttpContext? httpContext, string nonce);
}

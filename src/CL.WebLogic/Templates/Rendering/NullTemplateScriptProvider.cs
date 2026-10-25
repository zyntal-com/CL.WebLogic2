using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Templates.Rendering;

/// <summary>Placeholder until the client runtime is registered: emits nothing.</summary>
public sealed class NullTemplateScriptProvider : ITemplateScriptProvider
{
    /// <inheritdoc/>
    public string GetScriptTags(HttpContext? httpContext, string nonce) => string.Empty;
}

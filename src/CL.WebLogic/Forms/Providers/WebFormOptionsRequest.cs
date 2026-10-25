using CL.WebLogic.Security.Identity;
using CL.WebLogic.Security.Pipeline;

namespace CL.WebLogic.Forms.Providers;

/// <summary>Who asks for options, and for which form field.</summary>
public sealed class WebFormOptionsRequest
{
    /// <summary>The caller.</summary>
    public WebIdentity Identity { get; init; } = WebIdentity.Anonymous;

    /// <summary>The request's security context (null outside HTTP).</summary>
    public WebSecurityContext? Security { get; init; }

    /// <summary>Form id, when known.</summary>
    public string? FormId { get; init; }

    /// <summary>Field name, when known.</summary>
    public string? Field { get; init; }
}

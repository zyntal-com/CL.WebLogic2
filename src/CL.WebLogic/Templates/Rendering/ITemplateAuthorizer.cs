using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Templates.Rendering;

/// <summary>Answers <c>{if:group:…}</c> and <c>{if:permission:…}</c>.</summary>
public interface ITemplateAuthorizer
{
    /// <summary>True when the request's user is in any of <paramref name="groups"/>.</summary>
    bool IsInAnyGroup(HttpContext? httpContext, IReadOnlyList<string> groups);

    /// <summary>True when the request's user holds <paramref name="permission"/>.</summary>
    bool HasPermission(HttpContext? httpContext, string permission);
}

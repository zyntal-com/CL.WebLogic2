using System.Globalization;
using System.Security.Claims;
using CL.WebLogic.Templates.Ast;

namespace CL.WebLogic.Templates.Rendering;

/// <summary>Resolves selectors against the render context.</summary>
public static class TemplateValueResolver
{
    /// <summary>Resolves <paramref name="selector"/>.</summary>
    public static object? Resolve(TemplateRenderContext context, TemplateSelector selector) => selector.Scope switch
    {
        TemplateScope.None => ResolveRoot(context, selector.Key),
        TemplateScope.Model => ResolveModel(context, selector.Key),
        TemplateScope.Item => TemplatePathWalker.Resolve(context.CurrentItem, selector.Key),
        TemplateScope.Param => context.Params is null ? null : TemplatePathWalker.Resolve(context.Params, selector.Key),
        TemplateScope.Loop => context.Loop?.Get(selector.Key),
        TemplateScope.Alias => selector.Alias is not null && context.Aliases is not null && context.Aliases.TryGetValue(selector.Alias, out var aliased)
            ? TemplatePathWalker.Resolve(aliased, selector.Key)
            : null,
        TemplateScope.Query => context.HttpContext?.Request.Query.TryGetValue(selector.Key, out var q) == true && q.Count > 0 ? q[0] : null,
        TemplateScope.Route => context.HttpContext?.Request.RouteValues.TryGetValue(selector.Key, out var r) == true
            ? Convert.ToString(r, CultureInfo.InvariantCulture)
            : null,
        TemplateScope.Page => ResolvePage(context, selector.Key),
        TemplateScope.User => ResolveUser(context.HttpContext?.User, selector.Key),
        _ => null
    };

    private static object? ResolveRoot(TemplateRenderContext context, string key)
    {
        var fromModel = TemplatePathWalker.Resolve(context.Model, key);
        return fromModel ?? TemplatePathWalker.Resolve(context.Globals, key);
    }

    private static object? ResolveModel(TemplateRenderContext context, string key) =>
        TemplatePathWalker.Resolve(context.Model, key) ?? TemplatePathWalker.Resolve(context.Globals, key);

    private static object? ResolvePage(TemplateRenderContext context, string key)
    {
        var request = context.HttpContext?.Request;
        return key.ToLowerInvariant() switch
        {
            "path" => request?.Path.Value,
            "path_base" => request?.PathBase.Value,
            "method" => request?.Method,
            "query_string" => request?.QueryString.Value,
            "is_https" => request?.IsHttps,
            "template" => context.TemplatePath,
            _ => context.Globals.TryGetValue(key, out var global) ? global : null
        };
    }

    private static object? ResolveUser(ClaimsPrincipal? user, string key)
    {
        if (user is null)
            return key.Equals("is_authenticated", StringComparison.OrdinalIgnoreCase) ? false : null;

        return key.ToLowerInvariant() switch
        {
            "is_authenticated" => user.Identity?.IsAuthenticated == true,
            "id" => user.FindFirst(ClaimTypes.NameIdentifier)?.Value,
            "name" => user.FindFirst(ClaimTypes.Name)?.Value ?? user.Identity?.Name,
            "email" => user.FindFirst(ClaimTypes.Email)?.Value,
            "groups" => user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList(),
            _ => user.FindFirst(key)?.Value
        };
    }
}

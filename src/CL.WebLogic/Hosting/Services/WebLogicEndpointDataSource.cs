using CL.WebLogic.Diagnostics.Services;
using CL.WebLogic.Routing.Models;
using CL.WebLogic.Routing.Services;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Primitives;

namespace CL.WebLogic.Hosting.Services;

/// <summary>
/// Projects the <see cref="WebRouteRegistry"/> into real ASP.NET endpoints. ASP.NET routing then
/// handles templates, constraints, precedence, 405 with <c>Allow</c>, and per-endpoint CORS.
/// The endpoint list is rebuilt whenever the registry changes (contributor added or removed).
/// </summary>
public sealed class WebLogicEndpointDataSource : EndpointDataSource
{
    private readonly WebRouteRegistry _registry;
    private readonly WebLogicMetrics? _metrics;
    private readonly object _lock = new();
    private IReadOnlyList<Endpoint> _endpoints = [];
    private long _builtVersion = -1;

    /// <summary>Creates the data source over the registry.</summary>
    public WebLogicEndpointDataSource(WebRouteRegistry registry)
        : this(registry, null)
    {
    }

    /// <summary>Creates the data source over the registry; route requests are measured through <paramref name="metrics"/>.</summary>
    public WebLogicEndpointDataSource(WebRouteRegistry registry, WebLogicMetrics? metrics)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _metrics = metrics;
    }

    /// <inheritdoc/>
    public override IReadOnlyList<Endpoint> Endpoints
    {
        get
        {
            var version = _registry.Version;
            if (Volatile.Read(ref _builtVersion) == version)
                return _endpoints;

            lock (_lock)
            {
                if (_builtVersion != version)
                {
                    _endpoints = _registry.Routes.Select(BuildEndpoint).ToList();
                    Volatile.Write(ref _builtVersion, version);
                }
                return _endpoints;
            }
        }
    }

    /// <inheritdoc/>
    public override IChangeToken GetChangeToken() => _registry.GetChangeToken();

    private Endpoint BuildEndpoint(WebRouteDefinition route)
    {
        var meta = route.Metadata;
        var builder = new RouteEndpointBuilder(
            WebRouteEndpointInvoker.Create(route, _metrics),
            RoutePatternFactory.Parse(meta.Pattern),
            route.Order)
        {
            DisplayName = $"WebLogic {meta.Kind} {string.Join(",", meta.Methods)} {meta.Pattern} ({meta.Contributor.Id})"
        };

        builder.Metadata.Add(meta);
        builder.Metadata.Add(new HttpMethodMetadata(WithHead(meta.Methods), acceptCorsPreflight: meta.CorsPolicy is not null));
        if (meta.OutputCachePolicy is not null)
            builder.Metadata.Add(new Microsoft.AspNetCore.OutputCaching.OutputCacheAttribute { PolicyName = meta.OutputCachePolicy });
        if (meta.CorsPolicy is not null)
            builder.Metadata.Add(new EnableCorsAttribute(meta.CorsPolicy));

        return builder.Build();
    }

    /// <summary>GET routes answer HEAD too (Kestrel suppresses the body).</summary>
    private static IReadOnlyList<string> WithHead(IReadOnlyList<string> methods) =>
        methods.Contains(HttpMethods.Get, StringComparer.Ordinal) && !methods.Contains(HttpMethods.Head, StringComparer.Ordinal)
            ? [.. methods, HttpMethods.Head]
            : methods;
}

using System.Text.Json;

namespace CL.WebLogic.Routing.Models;

/// <summary>A committed route: pattern, handler, metadata and route-scoped middleware.</summary>
public sealed class WebRouteDefinition
{
    /// <summary>Unique id of this registration.</summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>Immutable route description (also attached as endpoint metadata).</summary>
    public required WebRouteMetadata Metadata { get; init; }

    /// <summary>The request handler.</summary>
    public required WebRouteHandler Handler { get; init; }

    /// <summary>Route-scoped middleware, outermost first.</summary>
    public IReadOnlyList<IWebMiddleware> Middleware { get; init; } = [];

    /// <summary>ASP.NET endpoint order (lower wins among equally specific patterns).</summary>
    public int Order { get; init; }

    /// <summary>
    /// JSON options owned by the contributor. Kept per contributor so the serializer's type
    /// metadata cache never roots an unloaded plugin's types.
    /// </summary>
    public required JsonSerializerOptions JsonOptions { get; init; }
}

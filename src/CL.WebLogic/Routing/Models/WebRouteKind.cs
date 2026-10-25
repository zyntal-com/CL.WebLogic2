namespace CL.WebLogic.Routing.Models;

/// <summary>What a registered route produces. Drives defaults (methods, error format, explorer grouping).</summary>
public enum WebRouteKind
{
    /// <summary>A full HTML page.</summary>
    Page,

    /// <summary>A JSON (or other machine-readable) API endpoint. Errors are rendered as ProblemDetails.</summary>
    Api,

    /// <summary>An HTML fragment swapped into an existing page by the client.</summary>
    Fragment,

    /// <summary>A server-sent events stream.</summary>
    Sse,

    /// <summary>The catch-all route that runs when nothing else matched.</summary>
    Fallback,

    /// <summary>Theme or client static assets.</summary>
    Asset,

    /// <summary>Toolkit-owned infrastructure routes (health, explorer, realtime).</summary>
    System
}

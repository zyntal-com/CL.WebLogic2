using CL.WebLogic.Runtime.Models;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Results;

/// <summary>Adapts an ASP.NET <see cref="IResult"/> so any built-in result can be returned from a WebLogic route.</summary>
public sealed class AspNetResult : WebResult
{
    /// <summary>Creates the adapter.</summary>
    public AspNetResult(IResult inner) => Inner = inner ?? throw new ArgumentNullException(nameof(inner));

    /// <summary>The wrapped result.</summary>
    public IResult Inner { get; }

    /// <inheritdoc/>
    public override Task ExecuteAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        foreach (var (name, value) in Headers)
            request.HttpContext.Response.Headers[name] = value;
        return Inner.ExecuteAsync(request.HttpContext);
    }
}

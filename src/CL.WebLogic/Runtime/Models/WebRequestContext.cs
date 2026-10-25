using CL.WebLogic.Security.Identity;
using CL.WebLogic.Security.Pipeline;
using CL.WebLogic.Security.Sessions;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CL.WebLogic.Routing.Models;
using CL.WebLogic.Runtime.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace CL.WebLogic.Runtime.Models;

/// <summary>
/// Per-request context passed to WebLogic route handlers. A thin, allocation-light view over
/// <see cref="Microsoft.AspNetCore.Http.HttpContext"/> plus the matched route's metadata.
/// One instance per request — never shared, never stored statically.
/// </summary>
public sealed class WebRequestContext
{
    private IFormCollection? _form;
    private Auth.SignIn.WebRequestAuth? _auth;
    private Forms.Services.WebRequestForms? _forms;
    private Caching.Services.WebRequestCache? _cache;

    /// <summary>Creates the context for a matched WebLogic route.</summary>
    public WebRequestContext(HttpContext httpContext, WebRouteMetadata route, JsonSerializerOptions jsonOptions)
    {
        HttpContext = httpContext ?? throw new ArgumentNullException(nameof(httpContext));
        Route = route ?? throw new ArgumentNullException(nameof(route));
        JsonOptions = jsonOptions ?? throw new ArgumentNullException(nameof(jsonOptions));
    }

    /// <summary>The underlying ASP.NET context.</summary>
    public HttpContext HttpContext { get; }

    /// <summary>Metadata of the matched route.</summary>
    public WebRouteMetadata Route { get; }

    /// <summary>JSON options of the contributor that owns the route.</summary>
    public JsonSerializerOptions JsonOptions { get; }

    /// <summary>Request-scoped services.</summary>
    public IServiceProvider Services => HttpContext.RequestServices;

    /// <summary>Signalled when the client disconnects.</summary>
    public CancellationToken RequestAborted => HttpContext.RequestAborted;

    /// <summary>HTTP method (upper case).</summary>
    public string Method => HttpContext.Request.Method;

    /// <summary>Request path (without path base).</summary>
    public string Path => HttpContext.Request.Path.Value ?? "/";

    /// <summary>Raw query string including the leading <c>?</c>, or empty.</summary>
    public string QueryString => HttpContext.Request.QueryString.Value ?? string.Empty;

    /// <summary>True for HTTPS requests (after trusted forwarded headers were applied).</summary>
    public bool IsHttps => HttpContext.Request.IsHttps;

    /// <summary>The authenticated principal (anonymous when nobody is signed in).</summary>
    public ClaimsPrincipal User => HttpContext.User;

    /// <summary>True when the caller is authenticated.</summary>
    public bool IsAuthenticated => User.Identity?.IsAuthenticated == true;

    /// <summary>The security state of the request (identity, client IP, tenant, header overrides).</summary>
    public WebSecurityContext Security =>
        WebSecurityContext.Get(HttpContext) ?? throw new InvalidOperationException("The WebLogic security pipeline did not run for this request.");

    /// <summary>Who is calling (anonymous when not signed in).</summary>
    public WebIdentity Identity => WebSecurityContext.Get(HttpContext)?.Identity ?? WebIdentity.Anonymous;

    /// <summary>Sign-in, second factor, sign-out and password flows for this request.</summary>
    public Auth.SignIn.WebRequestAuth Auth => _auth ??= new Auth.SignIn.WebRequestAuth(this, GetRequiredService<Auth.SignIn.WebAuthService>());

    /// <summary>Forms: bind, validate, prefill and render.</summary>
    public Forms.Services.WebRequestForms Forms => _forms ??= new Forms.Services.WebRequestForms(this, GetRequiredService<Forms.Services.WebFormService>());

    /// <summary>Fragment cache scoped to the tenant, and tag eviction.</summary>
    public Caching.Services.WebRequestCache Cache => _cache ??= new Caching.Services.WebRequestCache(this, GetRequiredService<Caching.Services.IWebFragmentCache>(), GetRequiredService<Caching.Services.IWebCacheInvalidator>());

    /// <summary>The visitor's session (created and stored only when written).</summary>
    public IWebSession Session =>
        HttpContext.Features.Get<IWebSession>() ?? throw new InvalidOperationException("Sessions are not available: the WebLogic security pipeline did not run for this request.");

    /// <summary>Client IP address (after trusted forwarded headers were applied), or null.</summary>
    public string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    /// <summary>True when the request was sent by the WebLogic JS client (<c>X-Requested-With: WebLogic</c>).</summary>
    public bool IsWebLogicClient =>
        string.Equals(GetHeader("X-Requested-With"), "WebLogic", StringComparison.Ordinal);

    /// <summary>Per-request item bag.</summary>
    public IDictionary<object, object?> Items => HttpContext.Items;

    /// <summary>First value of a query parameter, or null.</summary>
    public string? GetQuery(string key) =>
        HttpContext.Request.Query.TryGetValue(key, out var value) && value.Count > 0 ? value[0] : null;

    /// <summary>All values of a query parameter.</summary>
    public IReadOnlyList<string> GetQueryValues(string key) =>
        HttpContext.Request.Query.TryGetValue(key, out var value) ? value.Where(v => v is not null).Select(v => v!).ToArray() : [];

    /// <summary>First value of a request header, or null.</summary>
    public string? GetHeader(string name) =>
        HttpContext.Request.Headers.TryGetValue(name, out var value) && value.Count > 0 ? value[0] : null;

    /// <summary>A request cookie, or null.</summary>
    public string? GetCookie(string name) =>
        HttpContext.Request.Cookies.TryGetValue(name, out var value) ? value : null;

    /// <summary>A route value as text, or null.</summary>
    public string? GetRouteValue(string name) =>
        HttpContext.Request.RouteValues.TryGetValue(name, out var value) ? Convert.ToString(value, CultureInfo.InvariantCulture) : null;

    /// <summary>Parses a route value; returns false when it is missing or does not parse.</summary>
    public bool TryGetRouteValue<T>(string name, out T value) where T : IParsable<T>
    {
        value = default!;
        var raw = GetRouteValue(name);
        if (raw is null || !T.TryParse(raw, CultureInfo.InvariantCulture, out var parsed))
            return false;

        value = parsed;
        return true;
    }

    /// <summary>Resolves a required service from the request scope.</summary>
    public T GetRequiredService<T>() where T : notnull => Services.GetRequiredService<T>();

    /// <summary>Resolves an optional service from the request scope.</summary>
    public T? GetService<T>() where T : class => Services.GetService<T>();

    /// <summary>Reads the form body (cached for the rest of the request). Empty when the body is not a form.</summary>
    public async Task<IFormCollection> ReadFormAsync(CancellationToken cancellationToken)
    {
        if (_form is not null)
            return _form;

        _form = HttpContext.Request.HasFormContentType
            ? await HttpContext.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false)
            : FormCollection.Empty;
        return _form;
    }

    /// <summary>Reads the body as UTF-8 text within the route's body limit.</summary>
    public Task<string> ReadBodyAsStringAsync(CancellationToken cancellationToken) =>
        WebRequestBodyReader.ReadStringAsync(HttpContext.Request, BodyLimit, cancellationToken);

    /// <summary>Deserializes a JSON body within the route's body limit using the contributor's JSON options.</summary>
    public Task<T?> ReadJsonAsync<T>(CancellationToken cancellationToken) =>
        WebRequestBodyReader.ReadJsonAsync<T>(HttpContext.Request, BodyLimit, JsonOptions, cancellationToken);

    private long BodyLimit => Route.MaxBodyBytes ?? WebRequestBodyReader.DefaultMaxBodyBytes;
}

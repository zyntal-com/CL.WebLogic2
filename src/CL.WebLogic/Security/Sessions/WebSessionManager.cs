using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Security.Audit;
using CL.WebLogic.Security.Identity;
using CL.WebLogic.Security.Pipeline;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Security.Sessions;

/// <summary>
/// Creates, loads, rotates and persists the per-request <see cref="LazyWebSession"/>. Session ids
/// rotate on every sign-in, sign-out and privilege change (<see cref="RotateAsync"/>).
/// </summary>
public sealed class WebSessionManager
{
    private readonly IWebSessionStore _store;
    private readonly WebLogicConfigHolder _config;
    private readonly IWebIpHasher _ipHasher;
    private readonly IWebLogicContext _context;
    private readonly TimeProvider _time;

    /// <summary>Creates the manager.</summary>
    public WebSessionManager(IWebSessionStore store, WebLogicConfigHolder config, IWebIpHasher ipHasher, IWebLogicContext context, TimeProvider? time = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _ipHasher = ipHasher ?? throw new ArgumentNullException(nameof(ipHasher));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _time = time ?? TimeProvider.System;
    }

    /// <summary>The session of the request, creating (and loading from the cookie) on first use.</summary>
    public async ValueTask<LazyWebSession> GetAsync(WebSecurityContext security, CancellationToken cancellationToken)
    {
        var httpContext = security.HttpContext;
        if (httpContext.Features.Get<IWebSession>() is LazyWebSession existing)
            return existing;

        var session = new LazyWebSession();
        httpContext.Features.Set<IWebSession>(session);
        httpContext.Response.OnStarting(static state =>
        {
            var (manager, ctx, s) = ((WebSessionManager, WebSecurityContext, LazyWebSession))state;
            return manager.PersistAsync(ctx, s);
        }, (this, security, session));

        var config = _config.Current.Security.Session;
        var token = httpContext.Request.Cookies[WebSessionCookies.SessionName(httpContext, config)];
        if (token is null)
            return session;

        if (!WebSessionTokens.IsWellFormed(token))
        {
            session.ExpireCookie = true;
            return session;
        }

        var record = await _store.GetAsync(WebSessionTokens.Hash(token), cancellationToken).ConfigureAwait(false);
        var now = _time.GetUtcNow();
        if (record is null || record.ExpiresAt <= now || record.AbsoluteExpiresAt <= now
            || (config.BindToClientIp && record.ClientIpHash != _ipHasher.Hash(security.ClientIp)))
        {
            if (record is not null && record.ExpiresAt <= now)
                await _store.DeleteAsync(record.IdHash, cancellationToken).ConfigureAwait(false);
            session.ExpireCookie = true;
            return session;
        }

        session.Token = token;
        session.LoadFrom(record);
        return session;
    }

    /// <summary>
    /// Signs the session in (or into the pending-second-factor stage): rotates the id, records the
    /// user and installs the identity for the rest of this request.
    /// </summary>
    public async Task SignInAsync(WebSecurityContext security, WebSignIn signIn, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(signIn);
        var session = await GetAsync(security, cancellationToken).ConfigureAwait(false);
        // Re-signing the same user (refresh, password change) keeps the absolute timeout running.
        var sameUser = session.AuthStage == WebSessionAuthStage.Authenticated && session.UserId == signIn.UserId && session.Created is not null;
        var now = _time.GetUtcNow();
        Rotate(session);
        session.AuthStage = signIn.Stage;
        session.UserId = signIn.UserId;
        session.UserName = signIn.UserName;
        session.Groups = signIn.Groups;
        session.Permissions = signIn.Permissions;
        session.AuthenticationMethods = signIn.AuthenticationMethods;
        session.TenantId = signIn.TenantId;
        session.SecurityStamp = signIn.SecurityStamp;
        if (!sameUser)
            session.Created = now;
        session.AuthenticatedAt = signIn.AuthenticatedAt ?? (sameUser ? session.AuthenticatedAt ?? now : now);
        session.Persistent = signIn.Persistent ?? (sameUser && session.Persistent);
        session.UserAgent = UserAgent(security.HttpContext);
        session.Touch();

        security.Identity = signIn.Stage == WebSessionAuthStage.Authenticated ? ToIdentity(session) : WebIdentity.Anonymous;
        security.HttpContext.User = security.Identity.Principal;
        ExpirePreSessionCookie(security.HttpContext);
    }

    /// <summary>Signs out: ends this session (or every session of the user) and clears the identity.</summary>
    public async Task SignOutAsync(WebSecurityContext security, bool everywhere, CancellationToken cancellationToken)
    {
        var session = await GetAsync(security, cancellationToken).ConfigureAwait(false);
        if (everywhere && session.UserId is { } userId)
            await _store.DeleteForUserAsync(userId, cancellationToken).ConfigureAwait(false);

        // Forget the signed-in session entirely: a session write later in this request then
        // starts a fresh anonymous session under a new id instead of re-saving this one.
        if (session.Loaded is not null)
            session.PreviousIdHash ??= session.Loaded.IdHash;
        session.Loaded = null;
        session.Token = null;
        session.IssueCookie = false;
        session.CsrfSecret = null;
        session.Created = null;
        session.AuthStage = WebSessionAuthStage.Anonymous;
        session.UserId = null;
        session.UserName = null;
        session.Groups = [];
        session.Permissions = [];
        session.AuthenticationMethods = [];
        session.TenantId = null;
        session.SecurityStamp = null;
        session.AuthenticatedAt = null;
        session.Persistent = false;
        session.ClearValues();
        session.Abandoned = true;
        session.Dirty = false;
        security.Identity = WebIdentity.Anonymous;
        security.HttpContext.User = WebIdentity.Anonymous.Principal;
        ExpirePreSessionCookie(security.HttpContext);
    }

    /// <summary>Issues a new session id (and CSRF secret) keeping the data — call after any privilege change.</summary>
    public async Task RotateAsync(WebSecurityContext security, CancellationToken cancellationToken)
    {
        var session = await GetAsync(security, cancellationToken).ConfigureAwait(false);
        if (!session.IsPersisted)
            return;
        Rotate(session);
        session.Touch();
    }

    /// <summary>The identity of an authenticated session.</summary>
    public static WebIdentity ToIdentity(LazyWebSession session) => new()
    {
        UserId = session.UserId,
        UserName = session.UserName,
        Scheme = WebAuthenticationSchemes.Session,
        IsAmbient = true,
        Groups = session.Groups,
        Permissions = session.Permissions,
        AuthenticationMethods = session.AuthenticationMethods,
        TenantId = session.TenantId,
        AuthenticatedAt = session.AuthenticatedAt
    };

    /// <summary>The request's user agent, cut to what a device list needs.</summary>
    internal static string? UserAgent(HttpContext httpContext)
    {
        var value = httpContext.Request.Headers.UserAgent.ToString();
        if (value.Length == 0)
            return null;
        value = new string(value.Where(c => !char.IsControl(c)).Take(256).ToArray());
        return value;
    }

    /// <summary>CSRF tokens issued before a sign-in / sign-out must not survive it.</summary>
    private void ExpirePreSessionCookie(HttpContext httpContext)
    {
        var name = WebSessionCookies.Name(httpContext, _config.Current.Security.Csrf.PreSessionCookieName);
        if (httpContext.Request.Cookies.ContainsKey(name) && !httpContext.Response.HasStarted)
            httpContext.Response.Cookies.Delete(name, WebSessionCookies.Options(httpContext, null));
    }

    private static void Rotate(LazyWebSession session)
    {
        if (session.Loaded is not null)
            session.PreviousIdHash ??= session.Loaded.IdHash;
        session.Loaded = null;
        session.Token = WebSessionTokens.NewToken();
        session.IssueCookie = true;
        session.CsrfSecret = WebSessionTokens.NewSecret();
    }

    private async Task PersistAsync(WebSecurityContext security, LazyWebSession session)
    {
        var httpContext = security.HttpContext;
        var config = _config.Current.Security.Session;
        var cookieName = WebSessionCookies.SessionName(httpContext, config);
        var cancellationToken = CancellationToken.None; // the response is being written; finish the save

        try
        {
            if (session.PreviousIdHash is { } previous)
                await _store.DeleteAsync(previous, cancellationToken).ConfigureAwait(false);

            if (session.Abandoned)
            {
                if (session.Loaded is not null)
                    await _store.DeleteAsync(session.Loaded.IdHash, cancellationToken).ConfigureAwait(false);
                httpContext.Response.Cookies.Delete(cookieName, WebSessionCookies.Options(httpContext, null));
                return;
            }

            var now = _time.GetUtcNow();
            var touchDue = session.Loaded is not null && now - session.Loaded.LastSeenAt >= TimeSpan.FromSeconds(config.TouchIntervalSeconds);
            if (!session.Dirty && !touchDue)
            {
                if (session.ExpireCookie)
                    httpContext.Response.Cookies.Delete(cookieName, WebSessionCookies.Options(httpContext, null));
                return;
            }

            var created = session.Created ?? now;
            var absolute = session.Loaded?.AbsoluteExpiresAt
                ?? (session.Persistent ? created.AddDays(config.PersistentAbsoluteTimeoutDays) : created.AddHours(config.AbsoluteTimeoutHours));
            var idle = session.Persistent ? now.AddDays(config.PersistentIdleTimeoutDays) : now.AddMinutes(config.IdleTimeoutMinutes);
            var record = new WebSessionRecord
            {
                IdHash = WebSessionTokens.Hash(session.Token!),
                CreatedAt = created,
                LastSeenAt = now,
                AbsoluteExpiresAt = absolute,
                ExpiresAt = idle < absolute ? idle : absolute,
                AuthStage = session.AuthStage,
                UserId = session.UserId,
                UserName = session.UserName,
                Groups = session.Groups,
                Permissions = session.Permissions,
                AuthenticationMethods = session.AuthenticationMethods,
                TenantId = session.TenantId,
                SecurityStamp = session.SecurityStamp,
                AuthenticatedAt = session.AuthenticatedAt,
                Persistent = session.Persistent,
                UserAgent = session.UserAgent ??= UserAgent(httpContext),
                CsrfSecret = session.CsrfSecret ?? WebSessionTokens.NewSecret(),
                ClientIpHash = config.BindToClientIp ? _ipHasher.Hash(security.ClientIp) : null,
                Values = new Dictionary<string, string>(session.Values, StringComparer.Ordinal)
            };
            await _store.SaveAsync(record, cancellationToken).ConfigureAwait(false);

            // "Remember me" sessions survive a browser restart; others end with the browser session.
            if (session.IssueCookie)
                httpContext.Response.Cookies.Append(cookieName, session.Token!, WebSessionCookies.Options(httpContext, session.Persistent ? absolute : null));
        }
        catch (Exception ex)
        {
            _context.Logger.Error($"Saving the session failed [{httpContext.TraceIdentifier}]: {ex.Message}", ex);
        }
    }
}

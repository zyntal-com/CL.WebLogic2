using System.Globalization;
using CL.WebLogic.Auth.Events;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Security.Pipeline;
using CodeLogic.Core.Events;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace CL.WebLogic.Security.Audit;

/// <summary>
/// Writes account-security changes to the audit log: registration, password change or reset, e-mail change,
/// two-factor on/off, recovery codes regenerated or used, external logins linked or unlinked, API keys and
/// lockouts. (Sign-in, sign-out, passkeys, re-authentication and session revokes are recorded where they happen.)
/// The events are raised inline, so the entry carries the request (IP hash, path, trace id) when there is one;
/// changes made outside a request are recorded with the user id only.
/// </summary>
public sealed class WebAccountAuditRecorder : IHostedService
{
    private readonly IWebLogicContext _context;
    private readonly IWebAuditLog _audit;
    private readonly IHttpContextAccessor? _http;
    private readonly TimeProvider _time;
    private readonly List<IDisposable> _subscriptions = [];

    /// <summary>Creates the recorder.</summary>
    public WebAccountAuditRecorder(IWebLogicContext context, IWebAuditLog audit, IHttpContextAccessor? http = null, TimeProvider? time = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        _http = http;
        _time = time ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        On<UserRegisteredEvent>(e => Record(e.UserId, "auth.account", "registered"));
        On<UserDeletedEvent>(e =>
        {
            if (e.ByUserId is null)
                Record(e.UserId, "auth.account", "deleted");
            else
                Record(e.UserId, "auth.account", "deleted", ("by", e.ByUserId));
        });
        On<PasswordChangedEvent>(e => Record(e.UserId, "auth.password", e.ViaReset ? "reset" : "changed"));
        On<EmailChangedEvent>(e => Record(e.UserId, "auth.email", "changed"));
        On<TwoFactorChangedEvent>(e => Record(e.UserId, "auth.two-factor", e.Enabled ? "enabled" : "disabled"));
        On<RecoveryCodesRegeneratedEvent>(e => Record(e.UserId, "auth.two-factor", "recovery-codes-regenerated"));
        On<RecoveryCodeRedeemedEvent>(e => Record(e.UserId, "auth.two-factor", "recovery-code-used",
            ("remaining", e.Remaining.ToString(CultureInfo.InvariantCulture))));
        On<ExternalLoginChangedEvent>(e => Record(e.UserId, "auth.external-login", e.Linked ? "linked" : "unlinked", ("provider", e.Provider)));
        On<ApiKeyChangedEvent>(e => Record(e.UserId, "auth.api-key", e.Revoked ? "revoked" : "created", ("key", e.KeyId)));
        On<UserLockedOutEvent>(e => Record(e.UserId, "auth.lockout", "locked",
            ("until", e.Until.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture))));
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var subscription in _subscriptions)
            subscription.Dispose();
        _subscriptions.Clear();
        return Task.CompletedTask;
    }

    private void On<TEvent>(Action<TEvent> handler) where TEvent : IEvent =>
        _subscriptions.Add(_context.Events.Subscribe(handler));

    /// <summary>Records one entry; <c>user</c> is always in the data (the request may be anonymous, e.g. a reset link).</summary>
    internal void Record(string? userId, string category, string outcome, params (string Key, string Value)[] extra)
    {
        var data = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!string.IsNullOrEmpty(userId))
            data["user"] = userId;
        foreach (var (key, value) in extra)
            data[key] = value;

        if (_http?.HttpContext is { } httpContext && WebSecurityContext.Get(httpContext) is { } security)
        {
            _audit.Record(security, category, outcome, data: data);
            return;
        }
        _audit.Record(new WebAuditEntry
        {
            Timestamp = _time.GetUtcNow(),
            Category = category,
            Outcome = outcome,
            UserId = userId,
            Data = data
        });
    }
}

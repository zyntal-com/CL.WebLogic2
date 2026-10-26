namespace CL.WebLogic.MySql.Models;

/// <summary>The stores <c>UseMySqlStores</c> can register.</summary>
[Flags]
public enum WebMySqlStores
{
    /// <summary>None.</summary>
    None = 0,

    /// <summary><c>IWebUserStore</c> with lockout, e-mail and external-login capabilities.</summary>
    Users = 1,

    /// <summary><c>IWebSessionStore</c> with session listing.</summary>
    Sessions = 2,

    /// <summary><c>IWebApiKeyStore</c>.</summary>
    ApiKeys = 4,

    /// <summary><c>IWebRecoveryCodeStore</c>.</summary>
    RecoveryCodes = 8,

    /// <summary><c>IWebOneTimeUseStore</c> (TOTP replay protection, single-use links).</summary>
    OneTimeUse = 16,

    /// <summary><c>IWebLoginThrottle</c>.</summary>
    LoginThrottle = 32,

    /// <summary><c>IWebRateLimitStore</c> (one round trip per limited request).</summary>
    RateLimits = 64,

    /// <summary><c>IWebAuditSink</c>.</summary>
    Audit = 128,

    /// <summary><c>IWebWidgetSettingsStore</c>.</summary>
    WidgetSettings = 256,

    /// <summary><c>IWebDashboardLayoutStore</c>.</summary>
    Dashboards = 512,

    /// <summary><c>IWebPasskeyStore</c>.</summary>
    Passkeys = 1024,

    /// <summary>Every store.</summary>
    All = Users | Sessions | ApiKeys | RecoveryCodes | OneTimeUse | LoginThrottle | RateLimits | Audit | WidgetSettings | Dashboards | Passkeys
}

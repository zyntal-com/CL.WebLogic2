using System.Text.Json;
using CL.WebLogic.Runtime.Models;
using CL.WebLogic.Security.Sessions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Runtime.Services;

/// <summary>
/// One-shot messages for the next page: <see cref="Set"/> before a redirect, and the next page that renders
/// <c>{weblogic_scripts}</c> takes the message and the client shows it as a toast. Used by
/// <see cref="Results.FormResult"/> when a form is posted without the client, so its message is not lost.
/// The message rides in a short-lived cookie protected with DataProtection (it cannot be forged or read, and
/// works across nodes that share the key ring).
/// </summary>
public sealed class WebFlash
{
    /// <summary>Configured cookie name (without <c>__Host-</c> over plain HTTP).</summary>
    public const string CookieName = "__Host-wl_flash";

    /// <summary>Longest message kept; longer ones are cut (cookies are small).</summary>
    public const int MaxLength = 500;

    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(2);
    private static readonly string[] Variants = ["success", "info", "warning", "danger"];

    private readonly IDataProtector _protector;
    private readonly TimeProvider _time;

    /// <summary>Creates the service.</summary>
    public WebFlash(IDataProtectionProvider protection, TimeProvider? time = null)
    {
        _protector = (protection ?? throw new ArgumentNullException(nameof(protection))).CreateProtector("CL.WebLogic.Flash.v1");
        _time = time ?? TimeProvider.System;
    }

    /// <summary>
    /// Keeps <paramref name="message"/> for the next page. Empty messages and responses that already started
    /// are ignored; an unknown <paramref name="variant"/> becomes <c>info</c>.
    /// </summary>
    public void Set(HttpContext http, string? message, string variant = "success")
    {
        ArgumentNullException.ThrowIfNull(http);
        message = message?.Trim();
        if (string.IsNullOrEmpty(message) || http.Response.HasStarted)
            return;
        if (message.Length > MaxLength)
            message = message[..MaxLength];
        var expires = _time.GetUtcNow().Add(Lifetime);
        var payload = new Payload(message, Normalize(variant), expires.ToUnixTimeSeconds());
        http.Response.Cookies.Append(WebSessionCookies.Name(http, CookieName), _protector.Protect(JsonSerializer.Serialize(payload)),
            WebSessionCookies.Options(http, expires));
    }

    /// <summary>
    /// Returns the waiting message and removes it, or null. Tampered or expired cookies are removed too.
    /// Nothing is taken once the response has started (the cookie could not be cleared).
    /// </summary>
    public WebFlashMessage? Take(HttpContext http)
    {
        ArgumentNullException.ThrowIfNull(http);
        var name = WebSessionCookies.Name(http, CookieName);
        if (http.Request.Cookies[name] is not { Length: > 0 } cookie || http.Response.HasStarted)
            return null;
        http.Response.Cookies.Delete(name, WebSessionCookies.Options(http, null));

        Payload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<Payload>(_protector.Unprotect(cookie));
        }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or JsonException)
        {
            return null;
        }
        if (payload is null || string.IsNullOrEmpty(payload.M) || payload.E < _time.GetUtcNow().ToUnixTimeSeconds())
            return null;
        return new WebFlashMessage(payload.M, Normalize(payload.V));
    }

    /// <summary>True when the request carries a flash cookie (such pages are not served from the output cache).</summary>
    public static bool IsPending(HttpContext http)
    {
        ArgumentNullException.ThrowIfNull(http);
        return http.Request.Cookies.ContainsKey(WebSessionCookies.Name(http, CookieName));
    }

    private static string Normalize(string? variant) =>
        Variants.FirstOrDefault(v => string.Equals(v, variant, StringComparison.OrdinalIgnoreCase)) ?? "info";

    private sealed record Payload(string M, string V, long E);
}

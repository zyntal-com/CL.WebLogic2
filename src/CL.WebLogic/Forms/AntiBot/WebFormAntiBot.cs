using System.Globalization;
using System.Security.Cryptography;
using CL.WebLogic.Configuration.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Forms.AntiBot;

/// <summary>
/// Cheap bot protection without third parties: a hidden honeypot field that must stay empty and
/// a protected render timestamp (too fast = script, too old = replayed form).
/// </summary>
public sealed class WebFormAntiBot
{
    /// <summary>Honeypot field name.</summary>
    public const string HoneypotField = "__wl_hp";

    /// <summary>Timestamp field name.</summary>
    public const string TimestampField = "__wl_ts";

    private readonly IDataProtector _protector;
    private readonly WebLogicConfigHolder _config;
    private readonly TimeProvider _time;

    /// <summary>Creates the service.</summary>
    public WebFormAntiBot(IDataProtectionProvider protection, WebLogicConfigHolder config, TimeProvider? time = null)
    {
        _protector = (protection ?? throw new ArgumentNullException(nameof(protection))).CreateProtector("CL.WebLogic.Forms.Timestamp.v1");
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _time = time ?? TimeProvider.System;
    }

    /// <summary>The protected timestamp for <paramref name="formId"/>.</summary>
    public string IssueTimestamp(string formId) =>
        _protector.Protect(formId + "|" + _time.GetUtcNow().ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture));

    /// <summary>Null when the submission passes; otherwise the reason (for logs, never shown to users).</summary>
    public string? Check(string formId, IFormCollection form)
    {
        ArgumentNullException.ThrowIfNull(form);
        var config = _config.Current.Forms;
        if (config.Honeypot && form.TryGetValue(HoneypotField, out var honeypot) && !string.IsNullOrEmpty(honeypot.ToString()))
            return "honeypot";

        if (config.MinSubmitSeconds <= 0 && !form.ContainsKey(TimestampField))
            return null;
        string payload;
        try
        {
            payload = _protector.Unprotect(form[TimestampField].ToString());
        }
        catch (CryptographicException)
        {
            return "timestamp-invalid";
        }

        var bar = payload.LastIndexOf('|');
        if (bar < 0 || payload[..bar] != formId || !long.TryParse(payload[(bar + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var issued))
            return "timestamp-invalid";
        var age = _time.GetUtcNow() - DateTimeOffset.FromUnixTimeMilliseconds(issued);
        if (age < TimeSpan.FromSeconds(config.MinSubmitSeconds))
            return "too-fast";
        if (age > TimeSpan.FromMinutes(config.MaxFormAgeMinutes))
            return "expired";
        return null;
    }
}

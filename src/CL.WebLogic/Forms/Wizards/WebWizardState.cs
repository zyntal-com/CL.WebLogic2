using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Primitives;

namespace CL.WebLogic.Forms.Wizards;

/// <summary>
/// Multi-step progress carried in a protected hidden field (no server state, works across a
/// cluster): the form id, the current step and the values of the completed steps.
/// </summary>
public sealed class WebWizardState
{
    /// <summary>Hidden field name of the state token.</summary>
    public const string TokenField = "__wl_wizard";

    /// <summary>Name of the "back" submit button.</summary>
    public const string BackField = "__wl_wizard_back";

    private readonly IDataProtector _protector;
    private readonly TimeProvider _time;

    /// <summary>Creates the service.</summary>
    public WebWizardState(IDataProtectionProvider protection, TimeProvider? time = null)
    {
        _protector = (protection ?? throw new ArgumentNullException(nameof(protection))).CreateProtector("CL.WebLogic.Forms.Wizard.v1");
        _time = time ?? TimeProvider.System;
    }

    /// <summary>Protects the state.</summary>
    public string Protect(string formId, int step, IReadOnlyDictionary<string, StringValues> values)
    {
        var payload = new Payload
        {
            FormId = formId,
            Step = step,
            ExpiresAt = _time.GetUtcNow().AddHours(8).ToUnixTimeSeconds(),
            Values = values.ToDictionary(v => v.Key, v => v.Value.Select(s => s ?? "").ToArray())
        };
        return _protector.Protect(JsonSerializer.Serialize(payload));
    }

    /// <summary>Reads the state; null when missing, tampered, expired or for another form.</summary>
    public (int Step, Dictionary<string, StringValues> Values)? Read(string formId, string? token)
    {
        if (string.IsNullOrEmpty(token))
            return null;
        try
        {
            var payload = JsonSerializer.Deserialize<Payload>(_protector.Unprotect(token));
            if (payload is null || payload.FormId != formId || payload.ExpiresAt < _time.GetUtcNow().ToUnixTimeSeconds())
                return null;
            return (payload.Step, (payload.Values ?? []).ToDictionary(v => v.Key, v => new StringValues(v.Value), StringComparer.OrdinalIgnoreCase));
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException)
        {
            return null;
        }
    }

    private sealed class Payload
    {
        public string FormId { get; set; } = "";

        public int Step { get; set; }

        public long ExpiresAt { get; set; }

        public Dictionary<string, string[]>? Values { get; set; }
    }
}

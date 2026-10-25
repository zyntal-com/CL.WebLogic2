using CodeLogic.Core.Configuration;

namespace CL.WebLogic.Configuration.Models;

/// <summary>Cross-site request forgery protection for every unsafe request.</summary>
public sealed class CsrfConfig
{
    /// <summary>Header the client sends the token in.</summary>
    [ConfigField(Label = "Header Name", Group = "CSRF", Order = 90, Collapsed = true)]
    public string HeaderName { get; set; } = "X-CSRF-Token";

    /// <summary>Form field the token is posted in.</summary>
    [ConfigField(Label = "Field Name", Group = "CSRF", Order = 91, Collapsed = true)]
    public string FieldName { get; set; } = "__wl_csrf";

    /// <summary>Cookie holding the pre-session secret (login forms before a session exists).</summary>
    [ConfigField(Label = "Pre-session Cookie", Group = "CSRF", Order = 92, Collapsed = true)]
    public string PreSessionCookieName { get; set; } = "__Host-wl_pre";

    /// <summary>Other origins allowed to post (e.g. <c>https://admin.example.com</c>).</summary>
    [ConfigField(Label = "Trusted Origins", Description = "Additional origins allowed to send unsafe requests.", Group = "CSRF", Order = 93, Collapsed = true)]
    public List<string> TrustedOrigins { get; set; } = [];
}

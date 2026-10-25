using CodeLogic.Core.Configuration;

namespace CL.WebLogic.Configuration.Models;

/// <summary>
/// Security headers written on every response. The Content-Security-Policy uses a per-request
/// nonce with <c>'strict-dynamic'</c>; extra sources are added per directive.
/// </summary>
public sealed class ResponseHeadersConfig
{
    /// <summary>Send a Content-Security-Policy.</summary>
    [ConfigField(Label = "CSP Enabled", Group = "Headers", Order = 100)]
    public bool CspEnabled { get; set; } = true;

    /// <summary>Send the policy as <c>Content-Security-Policy-Report-Only</c>.</summary>
    [ConfigField(Label = "CSP Report Only", Group = "Headers", Order = 101)]
    public bool CspReportOnly { get; set; }

    /// <summary>Endpoint for CSP violation reports.</summary>
    [ConfigField(Label = "CSP Report URI", Group = "Headers", Order = 102, Collapsed = true)]
    public string CspReportUri { get; set; } = "";

    /// <summary>Extra <c>script-src</c> sources (with strict-dynamic, nonce-less scripts still need a nonce).</summary>
    [ConfigField(Label = "Script Sources", Group = "Headers", Order = 103, Collapsed = true)]
    public List<string> ScriptSources { get; set; } = [];

    /// <summary>Extra <c>style-src</c> sources.</summary>
    [ConfigField(Label = "Style Sources", Group = "Headers", Order = 104, Collapsed = true)]
    public List<string> StyleSources { get; set; } = [];

    /// <summary>Extra <c>img-src</c> sources.</summary>
    [ConfigField(Label = "Image Sources", Group = "Headers", Order = 105, Collapsed = true)]
    public List<string> ImageSources { get; set; } = [];

    /// <summary>Extra <c>connect-src</c> sources.</summary>
    [ConfigField(Label = "Connect Sources", Group = "Headers", Order = 106, Collapsed = true)]
    public List<string> ConnectSources { get; set; } = [];

    /// <summary>Extra <c>font-src</c> sources.</summary>
    [ConfigField(Label = "Font Sources", Group = "Headers", Order = 107, Collapsed = true)]
    public List<string> FontSources { get; set; } = [];

    /// <summary>Extra <c>frame-src</c> sources (embeds such as video players).</summary>
    [ConfigField(Label = "Frame Sources", Group = "Headers", Order = 108, Collapsed = true)]
    public List<string> FrameSources { get; set; } = [];

    /// <summary>Allow inline <c>style="…"</c> attributes (<c>style-src-attr 'unsafe-inline'</c>).</summary>
    [ConfigField(Label = "Allow Inline Style Attributes", Group = "Headers", Order = 109, Collapsed = true)]
    public bool AllowInlineStyleAttributes { get; set; }

    /// <summary>Origins allowed to frame the site (<c>frame-ancestors</c>); empty = none.</summary>
    [ConfigField(Label = "Frame Ancestors", Group = "Headers", Order = 110, Collapsed = true)]
    public List<string> FrameAncestors { get; set; } = [];

    /// <summary>Referrer-Policy value.</summary>
    [ConfigField(Label = "Referrer Policy", Group = "Headers", Order = 111, Collapsed = true)]
    public string ReferrerPolicy { get; set; } = "strict-origin-when-cross-origin";

    /// <summary>Permissions-Policy value.</summary>
    [ConfigField(Label = "Permissions Policy", Group = "Headers", Order = 112, Collapsed = true)]
    public string PermissionsPolicy { get; set; } = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";

    /// <summary>Cross-Origin-Opener-Policy value.</summary>
    [ConfigField(Label = "Cross-Origin-Opener-Policy", Group = "Headers", Order = 113, Collapsed = true)]
    public string CrossOriginOpenerPolicy { get; set; } = "same-origin";

    /// <summary>Cross-Origin-Resource-Policy value.</summary>
    [ConfigField(Label = "Cross-Origin-Resource-Policy", Group = "Headers", Order = 114, Collapsed = true)]
    public string CrossOriginResourcePolicy { get; set; } = "same-origin";

    /// <summary>Send <c>Cache-Control: no-store</c> on responses to signed-in users.</summary>
    [ConfigField(Label = "No-store When Authenticated", Group = "Headers", Order = 115, Collapsed = true)]
    public bool NoStoreWhenAuthenticated { get; set; } = true;
}

using System.ComponentModel.DataAnnotations;
using CL.WebLogic.Forms.Attributes;
using CL.WebLogic.Forms.Models;

namespace StarterWebsite.Forms;

/// <summary>Sign-in with a single-use recovery code instead of the authenticator.</summary>
[WebForm(Id = "recovery-code", SubmitLabel = "Use recovery code", AntiBot = false)]
public sealed class RecoveryCodeForm
{
    /// <summary>One of the codes shown when two-factor was enabled.</summary>
    [Required, StringLength(32)]
    [WebField(Label = "Recovery code", Autocomplete = "off", Placeholder = "xxxxx-xxxxx")]
    public string? Code { get; set; }

    /// <summary>Where to go afterwards (checked with GetSafeReturnUrl).</summary>
    [WebField(WebFieldType.Hidden)]
    public string? ReturnUrl { get; set; }
}

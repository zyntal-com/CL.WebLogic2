using System.ComponentModel.DataAnnotations;
using CL.WebLogic.Forms.Attributes;
using CL.WebLogic.Forms.Models;

namespace StarterWebsite.Forms;

/// <summary>The second step of sign-in: the authenticator code.</summary>
[WebForm(Id = "two-factor", SubmitLabel = "Verify", AntiBot = false)]
public sealed class TwoFactorForm
{
    /// <summary>Six digits from the authenticator app.</summary>
    [Required, RegularExpression("^[0-9 ]{6,8}$", ErrorMessage = "Enter the 6-digit code.")]
    [WebField(Label = "Authenticator code", Autocomplete = "one-time-code", Placeholder = "123456")]
    public string? Code { get; set; }

    /// <summary>Where to go afterwards (checked with GetSafeReturnUrl).</summary>
    [WebField(WebFieldType.Hidden)]
    public string? ReturnUrl { get; set; }
}

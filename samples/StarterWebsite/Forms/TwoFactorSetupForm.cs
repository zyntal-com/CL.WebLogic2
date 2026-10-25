using System.ComponentModel.DataAnnotations;
using CL.WebLogic.Forms.Attributes;
using CL.WebLogic.Forms.Models;

namespace StarterWebsite.Forms;

/// <summary>Confirms an authenticator enrollment with a first code.</summary>
[WebForm(Id = "two-factor-setup", SubmitLabel = "Turn on two-factor", AntiBot = false)]
public sealed class TwoFactorSetupForm
{
    /// <summary>The protected enrollment (secret + expiry) from BeginEnrollment.</summary>
    [Required, WebField(WebFieldType.Hidden)]
    public string? EnrollmentToken { get; set; }

    /// <summary>A code from the newly added authenticator entry.</summary>
    [Required, RegularExpression("^[0-9 ]{6,8}$", ErrorMessage = "Enter the 6-digit code.")]
    [WebField(Label = "Code from the app", Autocomplete = "one-time-code")]
    public string? Code { get; set; }
}

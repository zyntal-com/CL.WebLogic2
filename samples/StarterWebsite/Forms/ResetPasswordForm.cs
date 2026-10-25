using System.ComponentModel.DataAnnotations;
using CL.WebLogic.Forms.Attributes;
using CL.WebLogic.Forms.Models;

namespace StarterWebsite.Forms;

/// <summary>Choose a new password with the token from the e-mailed link.</summary>
[WebForm(Id = "reset-password", SubmitLabel = "Set new password", AntiBot = false)]
public sealed class ResetPasswordForm
{
    /// <summary>The reset token (single-use, bound to the account's security stamp).</summary>
    [Required, WebField(WebFieldType.Hidden)]
    public string? Token { get; set; }

    /// <summary>New password.</summary>
    [Required, StringLength(512, MinimumLength = 10)]
    [WebField(WebFieldType.Password, Label = "New password", Autocomplete = "new-password")]
    public string? NewPassword { get; set; }

    /// <summary>Repeat.</summary>
    [Required, Compare(nameof(NewPassword), ErrorMessage = "The passwords do not match.")]
    [WebField(WebFieldType.Password, Label = "Repeat password", Autocomplete = "new-password")]
    public string? ConfirmPassword { get; set; }
}

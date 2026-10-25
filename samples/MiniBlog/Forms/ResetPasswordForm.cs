using System.ComponentModel.DataAnnotations;
using CL.WebLogic.Forms.Attributes;
using CL.WebLogic.Forms.Models;

namespace MiniBlog.Forms;

/// <summary>New password from an e-mailed link.</summary>
[WebForm(Id = "reset-password", SubmitLabel = "Set new password", AntiBot = false)]
public sealed class ResetPasswordForm
{
    /// <summary>The reset token.</summary>
    [Required, WebField(WebFieldType.Hidden)]
    public string? Token { get; set; }

    /// <summary>New password.</summary>
    [Required, StringLength(512, MinimumLength = 10)]
    [WebField(WebFieldType.Password, Label = "New password", Autocomplete = "new-password")]
    public string? NewPassword { get; set; }
}

using System.ComponentModel.DataAnnotations;
using CL.WebLogic.Forms.Attributes;
using CL.WebLogic.Forms.Models;

namespace StarterWebsite.Forms;

/// <summary>Change the password while signed in (other sessions are signed out).</summary>
[WebForm(Id = "change-password", SubmitLabel = "Change password", AntiBot = false)]
public sealed class ChangePasswordForm
{
    /// <summary>Current password.</summary>
    [Required, StringLength(512)]
    [WebField(WebFieldType.Password, Label = "Current password", Autocomplete = "current-password")]
    public string? CurrentPassword { get; set; }

    /// <summary>New password.</summary>
    [Required, StringLength(512, MinimumLength = 10)]
    [WebField(WebFieldType.Password, Label = "New password", Autocomplete = "new-password")]
    public string? NewPassword { get; set; }
}
